namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Non-critical gaps in a posting (docs/08-site-playbooks.md §6.8). A
    /// posting missing these is kept and flagged; one missing a critical
    /// field (source, id, title, URL) is dropped instead. Bit values are
    /// stored and never repurposed.
    /// </summary>
    [Flags]
    public enum DataQualityIssues
    {
        /// <summary>
        /// No gaps.
        /// </summary>
        None = 0,

        /// <summary>
        /// No company name.
        /// </summary>
        MissingCompany = 1,

        /// <summary>
        /// No location.
        /// </summary>
        MissingLocation = 2,

        /// <summary>
        /// No unambiguously parsed salary.
        /// </summary>
        SalaryUnknown = 4,

        /// <summary>
        /// No parsed posted date.
        /// </summary>
        PostedDateUnknown = 8,

        /// <summary>
        /// No description.
        /// </summary>
        DescriptionMissing = 16,

        /// <summary>
        /// The parser fell back to low-confidence selectors.
        /// </summary>
        ParseLowConfidence = 32,
    }
}
