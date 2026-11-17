namespace JobScrape.Application.Validation
{
    /// <summary>
    /// Exact-name parsing for closed enums at request boundaries. Unlike
    /// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/>, numbers
    /// (<c>"01"</c>), comma lists (<c>"Csv,Xlsx"</c>), and flag combinations
    /// never parse: a request must name a declared member.
    /// </summary>
    public static class EnumNames
    {
        /// <summary>
        /// Matches a declared member name exactly, ignoring case and
        /// surrounding whitespace.
        /// </summary>
        /// <typeparam name="TEnum">The closed enum type.</typeparam>
        /// <param name="raw">The raw request text. Null never matches.</param>
        /// <param name="value">The matched member, or the default value.</param>
        /// <returns>True when <paramref name="raw"/> names a declared member.</returns>
        public static bool TryParse<TEnum>(string? raw, out TEnum value)
            where TEnum : struct, Enum
        {
            value = default;
            if (raw is null)
            {
                return false;
            }

            var trimmed = raw.Trim();
            foreach (var name in Enum.GetNames<TEnum>())
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    value = Enum.Parse<TEnum>(name);
                    return true;
                }
            }

            return false;
        }
    }
}
