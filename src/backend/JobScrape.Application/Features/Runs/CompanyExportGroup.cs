namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Which company slice of a run's postings to export.
    /// </summary>
    public enum CompanyExportGroup
    {
        /// <summary>Every posting the run saw.</summary>
        All,

        /// <summary>Only postings whose company matches the profile's list.</summary>
        Only,

        /// <summary>Postings whose company does not match the profile's list.</summary>
        Exclude,
    }
}
