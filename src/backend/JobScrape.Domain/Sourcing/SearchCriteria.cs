using JobScrape.Domain.Common;

namespace JobScrape.Domain.Sourcing
{
    /// <summary>
    /// The user-facing query of a search profile: keywords, locations, and
    /// optional experience and recency filters. Values are normalized on
    /// construction so two equivalent queries compare equal.
    /// </summary>
    public sealed record SearchCriteria
    {
        /// <summary>
        /// Maximum length of the normalized keyword string.
        /// </summary>
        public const int MaxKeywordsLength = 500;

        /// <summary>
        /// Maximum number of distinct locations.
        /// </summary>
        public const int MaxLocations = 10;

        /// <summary>
        /// Maximum number of distinct required skills. Higher than the
        /// location cap because key-skill pastes routinely list a full stack.
        /// </summary>
        public const int MaxRequiredSkills = 20;

        /// <summary>
        /// Maximum number of company names kept for the include and exclude
        /// exports. High enough for a large target list pasted at once.
        /// </summary>
        public const int MaxCompanies = 200;

        /// <summary>
        /// Initializes a new instance, normalizing and validating every field.
        /// </summary>
        /// <param name="keywords">
        /// Free-text keywords as the source query expects them, e.g.
        /// <c>"asp.net core c#"</c>. Trimmed with inner whitespace collapsed;
        /// must be non-blank and at most <see cref="MaxKeywordsLength"/> characters.
        /// </param>
        /// <param name="locations">
        /// Cities or regions. Blank entries are dropped, duplicates removed
        /// case-insensitively (first spelling wins); at most <see cref="MaxLocations"/>.
        /// </param>
        /// <param name="experienceYears">Optional years of experience, 0–50.</param>
        /// <param name="postedWithinDays">Optional recency window in days, 1–30.</param>
        /// <param name="remoteOnly">True to search remote roles only; then locations may be empty.</param>
        /// <param name="requiredSkills">
        /// Key skills; a posting is kept when any one of them matches one of
        /// its skills (see <see cref="SkillMatcher"/>). Blank entries are
        /// dropped, duplicates removed case-insensitively (first spelling
        /// wins); at most <see cref="MaxRequiredSkills"/>. Empty means no
        /// skill filter.
        /// </param>
        /// <param name="companies">
        /// Company names used to split the run export into an include file and
        /// an exclude file. Blank entries are dropped, duplicates removed
        /// case-insensitively (first spelling wins); at most
        /// <see cref="MaxCompanies"/>. Empty means no company split.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="locations"/> or <paramref name="requiredSkills"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when any value violates its invariant.</exception>
        public SearchCriteria(
            string? keywords,
            IEnumerable<string?> locations,
            int? experienceYears,
            int? postedWithinDays,
            bool remoteOnly,
            IEnumerable<string?> requiredSkills,
            IEnumerable<string?>? companies = null
        )
        {
            ArgumentNullException.ThrowIfNull(locations);
            ArgumentNullException.ThrowIfNull(requiredSkills);

            var normalizedKeywords = Text.Collapse(keywords);
            if (normalizedKeywords.Length == 0)
            {
                throw new DomainException("At least one keyword is required.");
            }

            if (normalizedKeywords.Length > MaxKeywordsLength)
            {
                throw new DomainException(
                    $"Keywords must be at most {MaxKeywordsLength} characters."
                );
            }

            var normalizedLocations = locations
                .Select(Text.Collapse)
                .Where(location => location.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedLocations.Length > MaxLocations)
            {
                throw new DomainException($"At most {MaxLocations} locations are allowed.");
            }
            // A search with neither a place nor the remote flag is unbounded
            // and would read the whole board.
            if (normalizedLocations.Length == 0 && !remoteOnly)
            {
                throw new DomainException("At least one location is required unless remote-only.");
            }

            if (experienceYears is < 0 or > 50)
            {
                throw new DomainException("experienceYears must be 0..50.");
            }

            if (postedWithinDays is < 1 or > 30)
            {
                throw new DomainException("postedWithinDays must be 1..30.");
            }

            var normalizedSkills = requiredSkills
                .Select(Text.Collapse)
                .Where(skill => skill.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedSkills.Length > MaxRequiredSkills)
            {
                throw new DomainException(
                    $"At most {MaxRequiredSkills} required skills are allowed."
                );
            }

            var normalizedCompanies = (companies ?? [])
                .Select(Text.Collapse)
                .Where(company => company.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedCompanies.Length > MaxCompanies)
            {
                throw new DomainException($"At most {MaxCompanies} companies are allowed.");
            }

            if (normalizedCompanies.Any(company => company.Length > CompanyName.MaxLength))
            {
                throw new DomainException(
                    $"Each company name must be at most {CompanyName.MaxLength} characters."
                );
            }

            Keywords = normalizedKeywords;
            Locations = normalizedLocations;
            ExperienceYears = experienceYears;
            PostedWithinDays = postedWithinDays;
            RemoteOnly = remoteOnly;
            RequiredSkills = normalizedSkills;
            Companies = normalizedCompanies;
        }

        /// <summary>
        /// Gets the normalized keyword string.
        /// </summary>
        public string Keywords { get; }

        /// <summary>
        /// Gets the distinct, normalized locations in their original order.
        /// </summary>
        public IReadOnlyList<string> Locations { get; }

        /// <summary>
        /// Gets the optional years-of-experience filter.
        /// </summary>
        public int? ExperienceYears { get; }

        /// <summary>
        /// Gets the optional posted-within-days filter.
        /// </summary>
        public int? PostedWithinDays { get; }

        /// <summary>
        /// Gets a value indicating whether only remote roles are searched.
        /// </summary>
        public bool RemoteOnly { get; }

        /// <summary>
        /// Gets the distinct, normalized required skills in their original
        /// order. Empty means no skill filter.
        /// </summary>
        public IReadOnlyList<string> RequiredSkills { get; }

        /// <summary>
        /// Gets the distinct company names used to split the run export into
        /// an include file and an exclude file. Empty means no company split.
        /// </summary>
        public IReadOnlyList<string> Companies { get; }

        /// <summary>
        /// Compares by value, including the location, required-skill, and
        /// company sequences. The compiler's record equality would compare
        /// the lists by reference.
        /// </summary>
        /// <param name="other">The criteria to compare with.</param>
        /// <returns>True when every field, location, skill, and company is equal.</returns>
        public bool Equals(SearchCriteria? other)
        {
            return other is not null
                && Keywords == other.Keywords
                && ExperienceYears == other.ExperienceYears
                && PostedWithinDays == other.PostedWithinDays
                && RemoteOnly == other.RemoteOnly
                && Locations.SequenceEqual(other.Locations, StringComparer.OrdinalIgnoreCase)
                && RequiredSkills.SequenceEqual(
                    other.RequiredSkills,
                    StringComparer.OrdinalIgnoreCase
                )
                && Companies.SequenceEqual(other.Companies, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns a hash consistent with <see cref="Equals(SearchCriteria)"/>.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Keywords, StringComparer.Ordinal);
            hash.Add(ExperienceYears);
            hash.Add(PostedWithinDays);
            hash.Add(RemoteOnly);
            foreach (var location in Locations)
            {
                hash.Add(location, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var skill in RequiredSkills)
            {
                hash.Add(skill, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var company in Companies)
            {
                hash.Add(company, StringComparer.OrdinalIgnoreCase);
            }

            return hash.ToHashCode();
        }
    }
}
