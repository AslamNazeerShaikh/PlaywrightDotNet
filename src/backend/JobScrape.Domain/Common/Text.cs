namespace JobScrape.Domain.Common
{
    /// <summary>
    /// Shared text normalization for value objects, so every type trims and
    /// collapses whitespace the same way.
    /// </summary>
    public static class Text
    {
        /// <summary>
        /// Trims and collapses each run of whitespace to one space.
        /// </summary>
        /// <param name="value">The raw text; null becomes empty.</param>
        /// <returns>The normalized text, possibly empty.</returns>
        public static string Collapse(string? value)
        {
            return value is null
                ? string.Empty
                : string.Join(
                    ' ',
                    value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                );
        }

        /// <summary>
        /// Collapses whitespace and maps an empty result to null, for
        /// optional fields where blank means absent.
        /// </summary>
        /// <param name="value">The raw text.</param>
        /// <returns>The normalized text, or null when blank.</returns>
        public static string? CollapseOrNull(string? value)
        {
            var collapsed = Collapse(value);
            return collapsed.Length == 0 ? null : collapsed;
        }

        /// <summary>
        /// Returns true when the text contains a control character. Run after
        /// <see cref="Collapse"/>, which has already removed tabs and newlines.
        /// </summary>
        /// <param name="value">The text to scan.</param>
        /// <returns>True when any character is a control character.</returns>
        public static bool HasControlCharacters(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return value.Any(char.IsControl);
        }
    }
}
