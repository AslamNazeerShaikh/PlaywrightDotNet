using ModelContextProtocol.AspNetCore;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The Streamable HTTP transport policy the API host and the
    /// composition test share. Stateless: no session affinity, no SSE
    /// endpoints, one server context per request.
    /// </summary>
    public static class McpHttpTransport
    {
        /// <summary>
        /// Configures the HTTP transport as stateless.
        /// </summary>
        /// <param name="options">The transport options. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public static void ConfigureStateless(HttpServerTransportOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Stateless = true;
        }
    }
}
