using System.Reflection;
using JobScrape.Mcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using Xunit;

namespace JobScrape.Mcp.Tests
{
    public sealed class McpCompositionTests
    {
        [Fact]
        public void ToolListResolvesTheThirteenDocumentedTools()
        {
            var builder = new ServiceCollection().AddMcpServer().WithTools(McpTools.All);

            Assert.NotNull(builder);
            var names = McpTools
                .All.SelectMany(type =>
                    type.GetMethods(
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                    )
                )
                .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
                .Where(attribute => attribute is not null)
                .Select(attribute => attribute!.Name ?? string.Empty)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                [
                    "cancel_run",
                    "create_profile",
                    "get_health",
                    "get_profile",
                    "get_run",
                    "get_session",
                    "list_profiles",
                    "list_run_postings",
                    "list_runs",
                    "list_sessions",
                    "list_sources",
                    "start_run",
                    "update_profile",
                ],
                names
            );
        }

        [Fact]
        public void HttpTransportIsStateless()
        {
            var services = new ServiceCollection();
            services.AddMcpServer().WithHttpTransport(McpHttpTransport.ConfigureStateless);

            var options = services
                .BuildServiceProvider()
                .GetRequiredService<IOptions<HttpServerTransportOptions>>()
                .Value;

            Assert.True(options.Stateless);
        }
    }
}
