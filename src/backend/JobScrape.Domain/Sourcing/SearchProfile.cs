using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Aggregate root for a reusable, validated search intent: which source
    /// to search, with what criteria, under which caps. Name uniqueness is a
    /// repository concern, enforced by the database.
    /// <para>
    /// High-risk sources start disabled and can only be enabled with explicit
    /// consent, so a high-risk profile can never be enabled by accident
    /// (docs/03-domain-model.md §4.1).
    /// </para>
    /// </summary>
    public sealed class SearchProfile : AggregateRoot
    {
        /// <summary>
        /// Initializes a new instance with already-validated state.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="name">The trimmed, non-empty name.</param>
        /// <param name="source">The searched source.</param>
        /// <param name="criteria">The search criteria.</param>
        /// <param name="limits">The run caps.</param>
        /// <param name="enabled">Whether runs may start.</param>
        /// <param name="createdAt">Creation time; also the initial update time.</param>
        private SearchProfile(
            SearchProfileId id,
            string name,
            SourceKey source,
            SearchCriteria criteria,
            RunLimits limits,
            bool enabled,
            DateTimeOffset createdAt
        )
        {
            Id = id;
            Name = name;
            Source = source;
            Criteria = criteria;
            Limits = limits;
            Enabled = enabled;
            CreatedAt = createdAt;
            UpdatedAt = createdAt;
        }

        /// <summary>
        /// Gets the profile identifier.
        /// </summary>
        public SearchProfileId Id { get; }

        /// <summary>
        /// Gets the operator-given name, trimmed.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the source this profile searches. Fixed for the profile's life.
        /// </summary>
        public SourceKey Source { get; }

        /// <summary>
        /// Gets the current search criteria.
        /// </summary>
        public SearchCriteria Criteria { get; private set; }

        /// <summary>
        /// Gets the current run caps; runs take this immutable snapshot.
        /// </summary>
        public RunLimits Limits { get; private set; }

        /// <summary>
        /// Gets a value indicating whether runs may start from this profile.
        /// </summary>
        public bool Enabled { get; private set; }

        /// <summary>
        /// Gets when the profile was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets when the profile last changed.
        /// </summary>
        public DateTimeOffset UpdatedAt { get; private set; }

        /// <summary>
        /// Creates a profile. It starts enabled unless the source is
        /// <see cref="ComplianceTier.HighRisk"/>, which needs explicit consent
        /// through <see cref="Enable"/>.
        /// </summary>
        /// <param name="name">The profile name. Trimmed; must be non-blank.</param>
        /// <param name="source">The source to search. Must not be null.</param>
        /// <param name="criteria">The search criteria. Must not be null.</param>
        /// <param name="limits">The run caps. Must not be null.</param>
        /// <param name="now">The creation time.</param>
        /// <returns>The new profile with a <see cref="SearchProfileCreated"/> event buffered.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a reference argument is null.</exception>
        /// <exception cref="DomainException">Thrown when <paramref name="name"/> is blank.</exception>
        public static SearchProfile Create(
            string? name,
            SourceKey source,
            SearchCriteria criteria,
            RunLimits limits,
            DateTimeOffset now
        )
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentNullException.ThrowIfNull(limits);

            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                throw new DomainException("A search profile name is required.");
            }

            var profile = new SearchProfile(
                SearchProfileId.New(),
                trimmed,
                source,
                criteria,
                limits,
                enabled: source.Tier != ComplianceTier.HighRisk,
                now
            );
            profile.Raise(new SearchProfileCreated(profile.Id, source, now));
            return profile;
        }

        /// <summary>
        /// Replaces the criteria. A value-equal replacement is a no-op.
        /// </summary>
        /// <param name="criteria">The new criteria. Must not be null.</param>
        /// <param name="now">The change time.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
        public void UpdateCriteria(SearchCriteria criteria, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            if (criteria.Equals(Criteria))
            {
                return;
            }

            Criteria = criteria;
            Touch("criteria", now);
        }

        /// <summary>
        /// Replaces the run caps. A value-equal replacement is a no-op.
        /// </summary>
        /// <param name="limits">The new caps. Must not be null.</param>
        /// <param name="now">The change time.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="limits"/> is null.</exception>
        public void UpdateLimits(RunLimits limits, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(limits);
            if (limits.Equals(Limits))
            {
                return;
            }

            Limits = limits;
            Touch("limits", now);
        }

        /// <summary>
        /// Enables the profile so runs may start. Enabling an enabled profile
        /// is a no-op.
        /// </summary>
        /// <param name="highRiskConsentAcknowledged">
        /// True when the operator explicitly accepted the risk of a
        /// <see cref="ComplianceTier.HighRisk"/> source. Ignored for other tiers.
        /// </param>
        /// <param name="now">The change time.</param>
        /// <exception cref="DomainException">
        /// Thrown when the source is high-risk and consent is not acknowledged.
        /// Callers check the compliance policy first and return a typed
        /// failure; reaching this throw is a programming error.
        /// </exception>
        public void Enable(bool highRiskConsentAcknowledged, DateTimeOffset now)
        {
            if (Source.Tier == ComplianceTier.HighRisk && !highRiskConsentAcknowledged)
            {
                throw new DomainException(
                    $"Source '{Source}' is high-risk; enabling requires explicit consent."
                );
            }

            if (Enabled)
            {
                return;
            }

            Enabled = true;
            Touch("enabled", now);
        }

        /// <summary>
        /// Disables the profile so no new runs start. Disabling a disabled
        /// profile is a no-op.
        /// </summary>
        /// <param name="now">The change time.</param>
        public void Disable(DateTimeOffset now)
        {
            if (!Enabled)
            {
                return;
            }

            Enabled = false;
            Touch("enabled", now);
        }

        /// <summary>
        /// Records a real change: bumps the update time and raises the event.
        /// </summary>
        /// <param name="changedField">The changed field name.</param>
        /// <param name="now">The change time.</param>
        private void Touch(string changedField, DateTimeOffset now)
        {
            UpdatedAt = now;
            Raise(new SearchProfileUpdated(Id, changedField, now));
        }
    }
}
