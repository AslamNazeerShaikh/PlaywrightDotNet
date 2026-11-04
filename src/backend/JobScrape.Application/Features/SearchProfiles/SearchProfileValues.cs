using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// A validated, normalized search-profile field set ready for the domain.
    /// </summary>
    /// <param name="Name">The trimmed unique name.</param>
    /// <param name="Source">The configured source.</param>
    /// <param name="Criteria">The validated search criteria.</param>
    /// <param name="Limits">The validated run caps.</param>
    public sealed record SearchProfileValues(
        string Name,
        SourceKey Source,
        SearchCriteria Criteria,
        RunLimits Limits
    );
}
