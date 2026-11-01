using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// Aggregate root for a normalized, deduplicated job posting. Identity is
    /// <c>(Source, ExternalJobId)</c> and never changes; a posting is not
    /// owned by any run (docs/03-domain-model.md §4.3).
    /// <para>
    /// Refreshing merges: a field the new snapshot did not observe (null, or
    /// an empty skill list) keeps its stored value, so a search-card
    /// sighting never erases what detail enrichment added. Every snapshot is
    /// fully validated before any field changes.
    /// </para>
    /// </summary>
    public sealed class JobPosting : AggregateRoot
    {
        /// <summary>
        /// Maximum stored description (and snippet) length. Adapters cap
        /// before mapping; the domain rejects anything longer.
        /// </summary>
        public const int MaxDescriptionLength = 20_000;

        /// <summary>
        /// Maximum distinct skills per posting.
        /// </summary>
        public const int MaxSkills = 50;

        /// <summary>
        /// Maximum length of a stored external URL (apply or company page).
        /// </summary>
        public const int MaxExternalUrlLength = 2048;

        // Explicit apply link; ApplyUrl falls back to the canonical URL.
        private string? _applyUrl;

        /// <summary>
        /// Initializes a new instance with its identity; fields are applied
        /// by <see cref="Create"/>.
        /// </summary>
        /// <param name="id">The posting identifier.</param>
        /// <param name="source">The posting's source.</param>
        /// <param name="externalJobId">The source's job id.</param>
        /// <param name="url">The canonical URL.</param>
        /// <param name="title">The title.</param>
        /// <param name="firstSeenAt">First sighting time.</param>
        private JobPosting(
            JobPostingId id,
            SourceKey source,
            ExternalJobId externalJobId,
            CanonicalUrl url,
            JobTitle title,
            DateTimeOffset firstSeenAt
        )
        {
            Id = id;
            Source = source;
            ExternalJobId = externalJobId;
            Url = url;
            Title = title;
            FirstSeenAt = firstSeenAt;
            LastSeenAt = firstSeenAt;
            Status = JobPostingStatus.Active;
            Skills = [];
            ContentHash = ContentHash.Compute([]);
        }

        /// <summary>
        /// Gets the posting identifier.
        /// </summary>
        public JobPostingId Id { get; }

        /// <summary>
        /// Gets the source. Part of the immutable identity.
        /// </summary>
        public SourceKey Source { get; }

        /// <summary>
        /// Gets the source's job id. Part of the immutable identity.
        /// </summary>
        public ExternalJobId ExternalJobId { get; }

        /// <summary>
        /// Gets the canonical job URL.
        /// </summary>
        public CanonicalUrl Url { get; private set; }

        /// <summary>
        /// Gets the apply URL: the visible external apply link when one was
        /// seen, otherwise the canonical job URL.
        /// </summary>
        public string ApplyUrl => _applyUrl ?? Url.Value;

        /// <summary>
        /// Gets the title.
        /// </summary>
        public JobTitle Title { get; private set; }

        /// <summary>
        /// Gets the company, if known.
        /// </summary>
        public CompanyName? Company { get; private set; }

        /// <summary>
        /// Gets the company page URL, if known.
        /// </summary>
        public string? CompanyUrl { get; private set; }

        /// <summary>
        /// Gets the location, if known.
        /// </summary>
        public JobLocation? Location { get; private set; }

        /// <summary>
        /// Gets the experience text as shown, if known.
        /// </summary>
        public string? ExperienceRaw { get; private set; }

        /// <summary>
        /// Gets the parsed minimum years of experience, if known.
        /// </summary>
        public int? ExperienceMinYears { get; private set; }

        /// <summary>
        /// Gets the parsed maximum years of experience, if known.
        /// </summary>
        public int? ExperienceMaxYears { get; private set; }

        /// <summary>
        /// Gets the salary text as shown, if known.
        /// </summary>
        public string? SalaryRaw { get; private set; }

        /// <summary>
        /// Gets the parsed salary, if unambiguous.
        /// </summary>
        public SalaryRange? Salary { get; private set; }

        /// <summary>
        /// Gets the posted-date text as shown, if known.
        /// </summary>
        public string? PostedRaw { get; private set; }

        /// <summary>
        /// Gets the parsed posted date, if known.
        /// </summary>
        public DateTimeOffset? PostedAt { get; private set; }

        /// <summary>
        /// Gets the description, if known; at most <see cref="MaxDescriptionLength"/> characters.
        /// </summary>
        public string? Description { get; private set; }

        /// <summary>
        /// Gets the card snippet, if known.
        /// </summary>
        public string? Snippet { get; private set; }

        /// <summary>
        /// Gets the distinct skills, at most <see cref="MaxSkills"/>.
        /// </summary>
        public IReadOnlyList<JobSkill> Skills { get; private set; }

        /// <summary>
        /// Gets when the posting was first seen. Never changes.
        /// </summary>
        public DateTimeOffset FirstSeenAt { get; }

        /// <summary>
        /// Gets when the posting was last seen; never before <see cref="FirstSeenAt"/>.
        /// </summary>
        public DateTimeOffset LastSeenAt { get; private set; }

        /// <summary>
        /// Gets the fingerprint of the stable fields.
        /// </summary>
        public ContentHash ContentHash { get; private set; }

        /// <summary>
        /// Gets whether the posting is still listed.
        /// </summary>
        public JobPostingStatus Status { get; private set; }

        /// <summary>
        /// Gets the non-critical gaps in the posting.
        /// </summary>
        public DataQualityIssues QualityIssues { get; private set; }

        /// <summary>
        /// Creates a posting from its first sighting.
        /// </summary>
        /// <param name="snapshot">The extracted fields. Must not be null.</param>
        /// <param name="now">The sighting time.</param>
        /// <returns>The new, active posting.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when any field violates its invariant.</exception>
        public static JobPosting Create(JobSnapshot snapshot, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var fields = Validate(snapshot);

            var posting = new JobPosting(
                JobPostingId.New(),
                snapshot.Source,
                fields.ExternalJobId,
                fields.Url,
                fields.Title,
                now
            );
            posting.Merge(fields);
            posting.ContentHash = posting.ComputeHash();
            return posting;
        }

        /// <summary>
        /// Applies a new sighting: merges observed fields, refreshes
        /// <see cref="LastSeenAt"/>, reactivates the posting, and raises
        /// <see cref="JobPostingUpdated"/> only when the content hash changed.
        /// </summary>
        /// <param name="snapshot">The extracted fields; same identity. Must not be null.</param>
        /// <param name="now">The sighting time; not before <see cref="FirstSeenAt"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="snapshot"/> is null.</exception>
        /// <exception cref="DomainException">
        /// Thrown when the identity differs, <paramref name="now"/> precedes
        /// the first sighting, or any field violates its invariant.
        /// </exception>
        public void Refresh(JobSnapshot snapshot, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var fields = Validate(snapshot);
            if (snapshot.Source != Source || fields.ExternalJobId != ExternalJobId)
            {
                throw new DomainException("Snapshot identity does not match posting identity.");
            }

            if (now < FirstSeenAt)
            {
                throw new DomainException("A posting cannot be seen before its first sighting.");
            }

            var oldHash = ContentHash;
            Merge(fields);
            ContentHash = ComputeHash();

            // Sightings can be recorded out of order; LastSeenAt only moves forward.
            LastSeenAt = now > LastSeenAt ? now : LastSeenAt;
            Status = JobPostingStatus.Active;

            if (ContentHash != oldHash)
            {
                Raise(new JobPostingUpdated(Id, Source, oldHash, ContentHash, now));
            }
        }

        /// <summary>
        /// Marks the posting expired because the source explicitly said so.
        /// A later sighting reactivates it.
        /// </summary>
        public void MarkExpired()
        {
            Status = JobPostingStatus.Expired;
        }

        /// <summary>
        /// Validates and normalizes every snapshot field without touching the
        /// aggregate, so a bad snapshot leaves the posting unchanged.
        /// </summary>
        /// <param name="snapshot">The extracted fields.</param>
        /// <returns>The validated fields; null means not observed.</returns>
        /// <exception cref="DomainException">Thrown when any field violates its invariant.</exception>
        private static ValidatedFields Validate(JobSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot.Source);

            var (min, max) = (snapshot.ExperienceMinYears, snapshot.ExperienceMaxYears);
            if (min is < 0 or > 50 || max is < 0 or > 50 || min > max)
            {
                throw new DomainException("Experience years must be 0..50 with min <= max.");
            }

            return new ValidatedFields(
                ExternalJobId.Create(snapshot.ExternalJobId),
                CanonicalUrl.Create(snapshot.Url, snapshot.Source),
                JobTitle.Create(snapshot.Title),
                Text.CollapseOrNull(snapshot.Company) is { } company
                    ? CompanyName.Create(company)
                    : null,
                ExternalUrlOrNull(snapshot.CompanyUrl, "Company URL"),
                snapshot.Location,
                Text.CollapseOrNull(snapshot.ExperienceRaw),
                min,
                max,
                Text.CollapseOrNull(snapshot.SalaryRaw),
                snapshot.Salary,
                Text.CollapseOrNull(snapshot.PostedRaw),
                snapshot.PostedAt,
                LongTextOrNull(snapshot.Description, "Description"),
                LongTextOrNull(snapshot.Snippet, "Snippet"),
                DistinctSkills(snapshot.Skills),
                ExternalUrlOrNull(snapshot.ApplyUrl, "Apply URL"),
                snapshot.LowConfidence
            );
        }

        /// <summary>
        /// Validates an off-source link (apply or company page): absolute
        /// <c>http</c>/<c>https</c>, no credentials, bounded length. Any host
        /// is allowed because external ATS links live elsewhere.
        /// </summary>
        /// <param name="raw">The raw URL.</param>
        /// <param name="field">Field name for the error message.</param>
        /// <returns>The normalized absolute URL, or null when blank.</returns>
        /// <exception cref="DomainException">Thrown when the URL is unsafe or too long.</exception>
        private static string? ExternalUrlOrNull(string? raw, string field)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            if (
                trimmed.Length > MaxExternalUrlLength
                || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || uri.UserInfo.Length > 0
            )
            {
                throw new DomainException(
                    $"{field} must be an absolute http(s) URL without credentials."
                );
            }

            return uri.AbsoluteUri;
        }

        /// <summary>
        /// Trims long text, keeping its inner line breaks; blank means absent.
        /// </summary>
        /// <param name="raw">The raw text.</param>
        /// <param name="field">Field name for the error message.</param>
        /// <returns>The trimmed text, or null when blank.</returns>
        /// <exception cref="DomainException">Thrown when the text exceeds <see cref="MaxDescriptionLength"/>.</exception>
        private static string? LongTextOrNull(string? raw, string field)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            return trimmed.Length > MaxDescriptionLength
                ? throw new DomainException(
                    $"{field} must be at most {MaxDescriptionLength} characters."
                )
                : trimmed;
        }

        /// <summary>
        /// Builds the distinct skill list: blanks dropped, first spelling of
        /// each case-insensitive name kept, at most <see cref="MaxSkills"/>.
        /// </summary>
        /// <param name="raw">The raw skill tags.</param>
        /// <returns>The skills; empty when none were observed.</returns>
        /// <exception cref="DomainException">Thrown when more than <see cref="MaxSkills"/> distinct skills remain.</exception>
        private static JobSkill[] DistinctSkills(IReadOnlyList<string>? raw)
        {
            var skills = (raw ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(JobSkill.Create)
                .DistinctBy(skill => skill.NormalizedName, StringComparer.Ordinal)
                .ToArray();

            return skills.Length > MaxSkills
                ? throw new DomainException($"A posting can have at most {MaxSkills} skills.")
                : skills;
        }

        /// <summary>
        /// Copies validated fields onto the aggregate. Required fields always
        /// replace; optional fields replace only when observed.
        /// </summary>
        /// <param name="fields">The validated fields.</param>
        private void Merge(ValidatedFields fields)
        {
            Url = fields.Url;
            Title = fields.Title;
            Company = fields.Company ?? Company;
            CompanyUrl = fields.CompanyUrl ?? CompanyUrl;
            Location = fields.Location ?? Location;
            ExperienceRaw = fields.ExperienceRaw ?? ExperienceRaw;
            if (fields.ExperienceMinYears is not null || fields.ExperienceMaxYears is not null)
            {
                // The parsed pair is replaced together so min <= max holds.
                ExperienceMinYears = fields.ExperienceMinYears;
                ExperienceMaxYears = fields.ExperienceMaxYears;
            }

            SalaryRaw = fields.SalaryRaw ?? SalaryRaw;
            Salary = fields.Salary ?? Salary;
            PostedRaw = fields.PostedRaw ?? PostedRaw;
            PostedAt = fields.PostedAt ?? PostedAt;
            Description = fields.Description ?? Description;
            Snippet = fields.Snippet ?? Snippet;
            Skills = fields.Skills.Length > 0 ? fields.Skills : Skills;
            _applyUrl = fields.ApplyUrl ?? _applyUrl;
            QualityIssues = ComputeIssues(fields.LowConfidence);
        }

        /// <summary>
        /// Derives the data-quality issues from the current fields.
        /// </summary>
        /// <param name="lowConfidence">Whether the latest parse was low-confidence.</param>
        /// <returns>The issues.</returns>
        private DataQualityIssues ComputeIssues(bool lowConfidence)
        {
            var issues = DataQualityIssues.None;
            issues |= Company is null ? DataQualityIssues.MissingCompany : DataQualityIssues.None;
            issues |= Location is null ? DataQualityIssues.MissingLocation : DataQualityIssues.None;
            issues |= Salary is null ? DataQualityIssues.SalaryUnknown : DataQualityIssues.None;
            issues |= PostedAt is null
                ? DataQualityIssues.PostedDateUnknown
                : DataQualityIssues.None;
            issues |= Description is null
                ? DataQualityIssues.DescriptionMissing
                : DataQualityIssues.None;
            issues |= lowConfidence ? DataQualityIssues.ParseLowConfidence : DataQualityIssues.None;
            return issues;
        }

        /// <summary>
        /// Hashes the stable fields named in docs/02-architecture.md §12.6:
        /// title, company, location, raw salary, description, and skills
        /// (order-insensitive). Dates and URLs are excluded on purpose.
        /// </summary>
        /// <returns>The content hash.</returns>
        private ContentHash ComputeHash()
        {
            var skills = string.Join(
                '\n',
                Skills.Select(skill => skill.NormalizedName).Order(StringComparer.Ordinal)
            );
            return ContentHash.Compute([
                Title.Value,
                Company?.Value,
                Location?.Raw,
                SalaryRaw,
                Description,
                skills,
            ]);
        }

        /// <summary>
        /// Snapshot fields after validation; null means not observed.
        /// </summary>
        private sealed record ValidatedFields(
            ExternalJobId ExternalJobId,
            CanonicalUrl Url,
            JobTitle Title,
            CompanyName? Company,
            string? CompanyUrl,
            JobLocation? Location,
            string? ExperienceRaw,
            int? ExperienceMinYears,
            int? ExperienceMaxYears,
            string? SalaryRaw,
            SalaryRange? Salary,
            string? PostedRaw,
            DateTimeOffset? PostedAt,
            string? Description,
            string? Snippet,
            JobSkill[] Skills,
            string? ApplyUrl,
            bool LowConfidence
        );
    }
}
