namespace JobScrape.Infrastructure.Sources
{
    /// <summary>
    /// One shared percent-encoded query-pair encoder for the source URL
    /// builders. Callers use <see cref="Uri.AbsoluteUri"/> (not
    /// <see cref="Uri.ToString"/>, which unescapes) when navigating.
    /// </summary>
    internal static class QueryString
    {
        /// <summary>
        /// Formats one percent-encoded query pair.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The raw value.</param>
        /// <returns>The encoded <c>name=value</c> pair.</returns>
        internal static string Pair(string name, string value)
        {
            return $"{name}={Uri.EscapeDataString(value)}";
        }
    }
}
