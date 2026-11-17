using System.Text.Json;
using JobScrape.Api.Controllers;
using JobScrape.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The database explorer controller's defensive catch arms. The HTTP
    /// tests cover the failures a real SQLite file produces; these use an
    /// explorer port that throws the remaining exception types so every
    /// mapping is pinned.
    /// </summary>
    public sealed class DatabaseControllerErrorMappingTests
    {
        private static CancellationToken Ct => TestContext.Current.CancellationToken;

        private static readonly UpdateRowRequest Update = new(
            "1",
            new Dictionary<string, JsonElement>()
        );

        [Fact]
        public async Task SchemaMapsDefensiveKeyNotFoundAndArgumentFailures()
        {
            var keyMissing = await StatusAsync(
                new DatabaseController(
                    new ThrowingExplorer(new KeyNotFoundException("missing"))
                ).SchemaAsync("t", Ct)
            );
            var invalid = await StatusAsync(
                new DatabaseController(
                    new ThrowingExplorer(new ArgumentException("bad"))
                ).SchemaAsync("t", Ct)
            );

            Assert.Equal(StatusCodes.Status404NotFound, keyMissing);
            Assert.Equal(StatusCodes.Status400BadRequest, invalid);
        }

        [Fact]
        public async Task RowsMapsDefensiveKeyNotFoundAndArgumentFailures()
        {
            var keyMissing = await StatusAsync(
                new DatabaseController(
                    new ThrowingExplorer(new KeyNotFoundException("missing"))
                ).RowsAsync("t", 0, 10, Ct)
            );
            var invalid = await StatusAsync(
                new DatabaseController(
                    new ThrowingExplorer(new ArgumentException("bad"))
                ).RowsAsync("t", 0, 10, Ct)
            );

            Assert.Equal(StatusCodes.Status404NotFound, keyMissing);
            Assert.Equal(StatusCodes.Status400BadRequest, invalid);
        }

        [Fact]
        public async Task InsertMapsEveryFailureKind()
        {
            Assert.Equal(
                StatusCodes.Status404NotFound,
                await InsertStatusAsync(new TableNotFoundException("t"))
            );
            Assert.Equal(
                StatusCodes.Status404NotFound,
                await InsertStatusAsync(new KeyNotFoundException("missing"))
            );
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                await InsertStatusAsync(new ArgumentException("bad"))
            );
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                await InsertStatusAsync(new SqliteException("down", 1))
            );
        }

        [Fact]
        public async Task UpdateMapsEveryFailureKind()
        {
            Assert.Equal(
                StatusCodes.Status404NotFound,
                await UpdateStatusAsync(new TableNotFoundException("t"))
            );
            Assert.Equal(
                StatusCodes.Status404NotFound,
                await UpdateStatusAsync(new KeyNotFoundException("missing"))
            );
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                await UpdateStatusAsync(new ArgumentException("bad"))
            );
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                await UpdateStatusAsync(new SqliteException("down", 1))
            );
        }

        [Fact]
        public async Task DeleteMapsEveryFailureKind()
        {
            Assert.Equal(
                StatusCodes.Status404NotFound,
                await DeleteStatusAsync(new TableNotFoundException("t"))
            );
            Assert.Equal(
                StatusCodes.Status404NotFound,
                await DeleteStatusAsync(new KeyNotFoundException("missing"))
            );
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                await DeleteStatusAsync(new ArgumentException("bad"))
            );
            Assert.Equal(
                StatusCodes.Status400BadRequest,
                await DeleteStatusAsync(new SqliteException("down", 1))
            );
        }

        private static Task<int> InsertStatusAsync(Exception error)
        {
            var controller = new DatabaseController(new ThrowingExplorer(error));
            return StatusAsync(controller.InsertAsync("t", null, Ct));
        }

        private static Task<int> UpdateStatusAsync(Exception error)
        {
            var controller = new DatabaseController(new ThrowingExplorer(error));
            return StatusAsync(controller.UpdateAsync("t", Update, Ct));
        }

        private static Task<int> DeleteStatusAsync(Exception error)
        {
            var controller = new DatabaseController(new ThrowingExplorer(error));
            return StatusAsync(controller.DeleteAsync("t", "1", Ct));
        }

        private static async Task<int> StatusAsync(Task<IResult> pending)
        {
            var result = await pending;
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.RequestServices = new ServiceCollection()
                .AddLogging()
                .AddProblemDetails()
                .BuildServiceProvider();
            await result.ExecuteAsync(context);
            return context.Response.StatusCode;
        }

        private sealed class ThrowingExplorer(Exception error) : IDatabaseExplorer
        {
            public Task<IReadOnlyList<DatabaseTableInfo>> ListTablesAsync(
                CancellationToken cancellationToken
            )
            {
                return Task.FromException<IReadOnlyList<DatabaseTableInfo>>(error);
            }

            public Task<DatabaseTableSchema?> GetSchemaAsync(
                string tableName,
                CancellationToken cancellationToken
            )
            {
                return Task.FromException<DatabaseTableSchema?>(error);
            }

            public Task<DatabaseRowsResult> ListRowsAsync(
                string tableName,
                int offset,
                int limit,
                CancellationToken cancellationToken
            )
            {
                return Task.FromException<DatabaseRowsResult>(error);
            }

            public Task InsertRowAsync(
                string tableName,
                IReadOnlyDictionary<string, JsonElement> values,
                CancellationToken cancellationToken
            )
            {
                return Task.FromException(error);
            }

            public Task UpdateRowAsync(
                string tableName,
                string primaryKeyValue,
                IReadOnlyDictionary<string, JsonElement> values,
                CancellationToken cancellationToken
            )
            {
                return Task.FromException(error);
            }

            public Task DeleteRowAsync(
                string tableName,
                string primaryKeyValue,
                CancellationToken cancellationToken
            )
            {
                return Task.FromException(error);
            }
        }
    }
}
