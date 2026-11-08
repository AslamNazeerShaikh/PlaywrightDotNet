namespace JobScrape.Infrastructure.Configuration
{
    /// <summary>
    /// The five <c>JobScrape:Features</c> switches
    /// (docs/superpowers/specs/2026-11-08-feature-toggles-design.md). Every
    /// default preserves current behavior; <c>false</c> turns the named
    /// feature off, except <c>Authentication</c>, which is reserved for the
    /// anonymous-mode follow-up
    /// (docs/superpowers/plans/2026-11-08-anonymous-mode.md): the flag
    /// exists and defaults to <c>true</c>, but setting it <c>false</c>
    /// changes nothing in this branch. No cross-field rules; plain bools
    /// only.
    /// </summary>
    public sealed class FeaturesOptions
    {
        /// <summary>
        /// The configuration section.
        /// </summary>
        public const string SectionName = "JobScrape:Features";

        /// <summary>
        /// Gets a value indicating whether sign-in is required. Reserved for
        /// the anonymous-mode follow-up
        /// (docs/superpowers/plans/2026-11-08-anonymous-mode.md): setting it
        /// false currently has no effect.
        /// </summary>
        public bool Authentication { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether Serilog writes its sinks. When
        /// false, registration stays but no sinks are configured.
        /// </summary>
        public bool Logging { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether the health route is served.
        /// </summary>
        public bool Health { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether the CSV export writer is registered.
        /// </summary>
        public bool CsvExport { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether the Excel export writer is registered.
        /// </summary>
        public bool ExcelExport { get; init; } = true;
    }
}
