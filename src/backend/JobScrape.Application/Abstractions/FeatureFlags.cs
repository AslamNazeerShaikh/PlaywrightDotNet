namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// The five <c>JobScrape:Features</c> switches, bound once at host
    /// startup. <c>False</c> means the named feature is off. Plain values
    /// for the application layer; handlers take this record, never
    /// <c>IOptions&lt;T&gt;</c>.
    /// </summary>
    /// <param name="Authentication">Reserved for the anonymous-mode follow-up
    /// (docs/superpowers/plans/2026-11-08-anonymous-mode.md): defaults true;
    /// false currently changes nothing.</param>
    /// <param name="Logging">When false, Serilog registers with no sinks.</param>
    /// <param name="Health">When false, the health route returns the feature-disabled 404.</param>
    /// <param name="CsvExport">When false, the CSV export writer is not registered.</param>
    /// <param name="ExcelExport">When false, the Excel export writer is not registered.</param>
    public sealed record FeatureFlags(
        bool Authentication,
        bool Logging,
        bool Health,
        bool CsvExport,
        bool ExcelExport
    );
}
