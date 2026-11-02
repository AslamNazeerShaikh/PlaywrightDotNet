namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// A salary parsed from the raw text, only when unambiguous
    /// (docs/08-site-playbooks.md §6.2). The raw text is kept separately on
    /// the posting; unknown values stay absent rather than guessed.
    /// </summary>
    public sealed record SalaryRange
    {
        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="min">Lower bound, non-negative; null when only a maximum is stated.</param>
        /// <param name="max">Upper bound, non-negative and at least <paramref name="min"/>; null when only a minimum is stated.</param>
        /// <param name="currency">ISO 4217 code, e.g. <c>INR</c>; upper-cased.</param>
        /// <param name="period">The pay period.</param>
        /// <exception cref="DomainException">
        /// Thrown when both bounds are missing, a bound is negative,
        /// <paramref name="min"/> exceeds <paramref name="max"/>, the currency
        /// is not three letters, or the period is undefined.
        /// </exception>
        public SalaryRange(decimal? min, decimal? max, string? currency, SalaryPeriod period)
        {
            if (min is null && max is null)
            {
                throw new DomainException("A salary range needs at least one bound.");
            }

            if (min < 0 || max < 0)
            {
                throw new DomainException("Salary bounds must be non-negative.");
            }

            if (min > max)
            {
                throw new DomainException("Salary min must be <= max.");
            }

            var code = currency?.Trim().ToUpperInvariant() ?? string.Empty;
            if (code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
            {
                throw new DomainException("Salary currency must be a 3-letter ISO 4217 code.");
            }

            if (!Enum.IsDefined(period))
            {
                throw new DomainException($"Unknown salary period {period}.");
            }

            Min = min;
            Max = max;
            Currency = code;
            Period = period;
        }

        /// <summary>
        /// Gets the lower bound, if stated.
        /// </summary>
        public decimal? Min { get; }

        /// <summary>
        /// Gets the upper bound, if stated.
        /// </summary>
        public decimal? Max { get; }

        /// <summary>
        /// Gets the upper-case ISO 4217 currency code.
        /// </summary>
        public string Currency { get; }

        /// <summary>
        /// Gets the pay period.
        /// </summary>
        public SalaryPeriod Period { get; }
    }
}
