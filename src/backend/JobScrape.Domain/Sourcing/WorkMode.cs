namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Where the work happens, detected from the posting text. Values are
    /// never repurposed.
    /// </summary>
    public enum WorkMode
    {
        /// <summary>
        /// Not stated or not detectable.
        /// </summary>
        Unknown,

        /// <summary>
        /// At the employer's site.
        /// </summary>
        Onsite,

        /// <summary>
        /// Split between site and home.
        /// </summary>
        Hybrid,

        /// <summary>
        /// Fully remote / work from home.
        /// </summary>
        Remote,
    }
}
