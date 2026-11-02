namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Whether a posting is still listed. Absence from a search does not
    /// expire a posting in v1; only an explicit source signal does. Values
    /// are never repurposed.
    /// </summary>
    public enum JobPostingStatus
    {
        /// <summary>
        /// Not determined.
        /// </summary>
        Unknown,

        /// <summary>
        /// Seen on the source.
        /// </summary>
        Active,

        /// <summary>
        /// The source explicitly marked it closed or expired.
        /// </summary>
        Expired,
    }
}
