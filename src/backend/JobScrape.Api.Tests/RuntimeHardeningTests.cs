using System.Net;
using System.Text.Json;
using JobScrape.Api.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.InMemory;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class RuntimeHardeningTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        // ---- global exception handler ----

        [Fact]
        public async Task UnhandledExceptionBecomesASafe500WithTraceId()
        {
            var logger = new CapturingLogger();
            var context = new DefaultHttpContext { TraceIdentifier = "trace-1" };
            context.Request.Method = "GET";
            context.Request.Path = "/api/v1/boom";
            context.Response.Body = new MemoryStream();
            context.RequestServices =
                new Microsoft.Extensions.DependencyInjection.ServiceCollection()
                    .AddLogging()
                    .BuildServiceProvider();

            var handled = await new GlobalExceptionHandler(logger).TryHandleAsync(
                context,
                new InvalidOperationException("secret connection string /var/db"),
                Ct
            );

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
            Assert.StartsWith(
                "application/problem+json",
                context.Response.ContentType,
                StringComparison.Ordinal
            );
            context.Response.Body.Position = 0;
            using var body = await JsonDocument.ParseAsync(
                context.Response.Body,
                cancellationToken: Ct
            );
            Assert.Equal("INTERNAL_ERROR", body.RootElement.GetProperty("code").GetString());
            Assert.Equal("trace-1", body.RootElement.GetProperty("traceId").GetString());
            var raw = body.RootElement.GetRawText();
            Assert.DoesNotContain("secret", raw, StringComparison.Ordinal);
            Assert.DoesNotContain("/var/db", raw, StringComparison.Ordinal);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.IsType<InvalidOperationException>(entry.Exception);
        }

        [Fact]
        public async Task CancellationIsNotHandledOrLoggedAsAFailure()
        {
            var logger = new CapturingLogger();

            var handled = await new GlobalExceptionHandler(logger).TryHandleAsync(
                new DefaultHttpContext(),
                new OperationCanceledException(),
                Ct
            );

            Assert.False(handled);
            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void HandlerAndMiddlewareRejectNulls()
        {
            Assert.Throws<ArgumentNullException>(() => new GlobalExceptionHandler(null!));
            Assert.Throws<ArgumentNullException>(() => new CorrelationMiddleware(null!));
        }

        // ---- correlation ----

        [Theory]
        [InlineData("abc-123_XYZ", "abc-123_XYZ")]
        [InlineData("bad id\r\nInjected: yes", null)]
        [InlineData("", null)]
        public async Task CorrelationIdIsReusedOnlyWhenSafeAndScopedIntoLogs(
            string supplied,
            string? expected
        )
        {
            var sink = new InMemorySink();
            using var logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink)
                .CreateLogger();
            var middleware = new CorrelationMiddleware(_ =>
            {
                logger.Information("inside");
                return Task.CompletedTask;
            });
            var context = new DefaultHttpContext { TraceIdentifier = "req-7" };
            context.Request.Headers[CorrelationMiddleware.HeaderName] = supplied;

            await middleware.InvokeAsync(context);

            var echoed = context.Response.Headers[CorrelationMiddleware.HeaderName].ToString();
            if (expected is null)
            {
                Assert.NotEqual(supplied, echoed);
                Assert.Matches("^[0-9a-f]{32}$", echoed);
            }
            else
            {
                Assert.Equal(expected, echoed);
            }

            var logged = Assert.Single(sink.LogEvents);
            Assert.Equal($"\"{echoed}\"", logged.Properties["CorrelationId"].ToString());
            Assert.Equal("\"req-7\"", logged.Properties["RequestId"].ToString());
        }

        [Fact]
        public async Task OverlongCorrelationIdIsReplaced()
        {
            var middleware = new CorrelationMiddleware(_ => Task.CompletedTask);
            var context = new DefaultHttpContext();
            var tooLong = new string('a', CorrelationMiddleware.MaxLength + 1);
            context.Request.Headers[CorrelationMiddleware.HeaderName] = tooLong;

            await middleware.InvokeAsync(context);

            Assert.NotEqual(
                tooLong,
                context.Response.Headers[CorrelationMiddleware.HeaderName].ToString()
            );
        }

        // ---- end to end ----

        [Fact]
        public async Task RequestsAreLoggedAsCompactJsonAndFlushedWhenTheHostStops()
        {
            var factory = new ApiFactory { DeleteLogsOnDispose = false };
            try
            {
                using (var client = factory.CreateClient())
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/health");
                    request.Headers.Add(CorrelationMiddleware.HeaderName, "e2e-check-1");
                    using var response = await client.SendAsync(request, Ct);

                    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                    Assert.Equal(
                        "e2e-check-1",
                        response.Headers.GetValues(CorrelationMiddleware.HeaderName).Single()
                    );
                }

                // Stopping the host must flush the async sink: no polling.
                await factory.DisposeAsync();

                var events = ReadEvents(factory.LogDirectory);
                var logged = Assert.Single(
                    events,
                    e =>
                        e.TryGetProperty("CorrelationId", out var id)
                        && id.GetString() == "e2e-check-1"
                        && e.TryGetProperty("RequestPath", out _)
                );
                Assert.Equal("JobScrape.Api", logged.GetProperty("Application").GetString());
                Assert.Equal("/api/v1/health", logged.GetProperty("RequestPath").GetString());
                Assert.Equal(200, logged.GetProperty("StatusCode").GetInt32());
                Assert.True(logged.TryGetProperty("MachineName", out _));
                Assert.True(logged.TryGetProperty("ProcessId", out _));
                Assert.True(logged.TryGetProperty("@mt", out _));
            }
            finally
            {
                await factory.DisposeAsync();
                if (Directory.Exists(factory.LogDirectory))
                {
                    Directory.Delete(factory.LogDirectory, recursive: true);
                }
            }
        }

        private static JsonElement[] ReadEvents(string directory)
        {
            return Directory
                .GetFiles(directory, "jobscrape-api-*.jsonl")
                .SelectMany(File.ReadAllLines)
                .Where(line => line.Length > 0)
                .Select(line => JsonDocument.Parse(line).RootElement.Clone())
                .ToArray();
        }

        private sealed class CapturingLogger : ILogger<GlobalExceptionHandler>
        {
            public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            )
            {
                Entries.Add((logLevel, exception));
            }
        }
    }
}
