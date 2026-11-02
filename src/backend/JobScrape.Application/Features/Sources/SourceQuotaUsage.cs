namespace JobScrape.Application.Features.Sources
{
    /// <summary>
    /// Today's (UTC) quota usage; zero when no run has touched the day.
    /// </summary>
    /// <param name="Date">The UTC day, <c>yyyy-MM-dd</c>.</param>
    /// <param name="PagesUsed">Result pages fetched today.</param>
    /// <param name="PageCap">Daily page cap.</param>
    /// <param name="DetailsUsed">Detail pages visited today.</param>
    /// <param name="DetailCap">Daily detail cap.</param>
    /// <param name="RunsUsed">Runs started today.</param>
    /// <param name="RunCap">Daily run cap.</param>
    /// <param name="Challenges">Walls hit today.</param>
    public sealed record SourceQuotaUsage(
        string Date,
        int PagesUsed,
        int PageCap,
        int DetailsUsed,
        int DetailCap,
        int RunsUsed,
        int RunCap,
        int Challenges
    );
}
