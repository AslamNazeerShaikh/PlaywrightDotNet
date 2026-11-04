namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Shared browser-profile request rules. The enum parser matches declared
    /// member names exactly, so numbers and comma lists are rejected.
    /// </summary>
    internal static class SessionValidation
    {
        /// <summary>
        /// Matches a declared member name exactly (case-insensitively).
        /// <c>Enum.TryParse</c> is deliberately avoided: it also accepts
        /// numbers and comma lists, so <c>"Chromium,Firefox"</c> would OR
        /// into a defined value.
        /// </summary>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <param name="raw">The raw text.</param>
        /// <param name="value">The matched member.</param>
        /// <returns>True when <paramref name="raw"/> is exactly a member name.</returns>
        public static bool TryParseEnum<TEnum>(string raw, out TEnum value)
            where TEnum : struct, Enum
        {
            var name = Enum.GetNames<TEnum>()
                .FirstOrDefault(member =>
                    string.Equals(member, raw.Trim(), StringComparison.OrdinalIgnoreCase)
                );
            value = name is null ? default : Enum.Parse<TEnum>(name);
            return name is not null;
        }
    }
}
