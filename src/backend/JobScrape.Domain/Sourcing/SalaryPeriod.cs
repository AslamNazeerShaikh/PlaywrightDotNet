namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// The pay period a parsed salary amount refers to. Values are never
    /// repurposed.
    /// </summary>
    public enum SalaryPeriod
    {
        /// <summary>
        /// Per hour.
        /// </summary>
        Hour,

        /// <summary>
        /// Per day.
        /// </summary>
        Day,

        /// <summary>
        /// Per week.
        /// </summary>
        Week,

        /// <summary>
        /// Per month.
        /// </summary>
        Month,

        /// <summary>
        /// Per year (e.g. Indian "LPA" / per annum).
        /// </summary>
        Year,
    }
}
