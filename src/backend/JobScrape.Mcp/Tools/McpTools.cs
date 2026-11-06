namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The tool classes the API host registers. One fixed list for the
    /// single <c>WithTools</c> call: no assembly scanning,
    /// so "Find usages" on a tool class finds this list and its host calls.
    /// Declared as <see cref="IEnumerable{T}"/> on purpose: the MCP
    /// <c>WithTools</c> overloads resolve a more specific collection type
    /// to the instance overload and silently register zero tools.
    /// </summary>
    public static class McpTools
    {
        /// <summary>
        /// Gets the seven tool classes, in registration order.
        /// </summary>
        public static IEnumerable<Type> All { get; } =
        [
            typeof(HealthTools),
            typeof(SourcesTools),
            typeof(SessionsTools),
            typeof(SearchProfilesTools),
            typeof(SearchProfilesWriteTools),
            typeof(RunsTools),
            typeof(RunActionsTools),
        ];
    }
}
