using System.Text.Json;
using JobScrape.Domain.Common;
using JobScrape.Domain.Identity;
using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// Value converters between domain value objects and SQLite columns.
    /// Every read goes back through the domain's validating factory or
    /// constructor, so a corrupt row fails loudly on load instead of
    /// producing an invalid aggregate. Multi-field value objects are stored
    /// as JSON through private DTOs, never by serializing domain types.
    /// Converters for optional members are typed nullable, but EF Core never
    /// passes null to a converter: null stays a SQL NULL.
    /// </summary>
    public static class DomainConverters
    {
        /// <summary>
        /// <see cref="Domain.Common.SourceKey"/> as its lowercase key.
        /// </summary>
        public static readonly ValueConverter<SourceKey, string> SourceKey = new(
            key => key.Value,
            value => Domain.Common.SourceKey.Parse(value)
        );

        /// <summary>
        /// <see cref="Domain.Common.SearchProfileId"/> as a GUID.
        /// </summary>
        public static readonly ValueConverter<SearchProfileId, Guid> SearchProfileId = new(
            id => id.Value,
            value => Domain.Common.SearchProfileId.From(value)
        );

        /// <summary>
        /// <see cref="Domain.Common.ScrapeRunId"/> as a GUID.
        /// </summary>
        public static readonly ValueConverter<ScrapeRunId, Guid> ScrapeRunId = new(
            id => id.Value,
            value => Domain.Common.ScrapeRunId.From(value)
        );

        /// <summary>
        /// <see cref="Domain.Common.JobPostingId"/> as a GUID.
        /// </summary>
        public static readonly ValueConverter<JobPostingId, Guid> JobPostingId = new(
            id => id.Value,
            value => Domain.Common.JobPostingId.From(value)
        );

        /// <summary>
        /// <see cref="Domain.Common.BrowserProfileId"/> as a GUID.
        /// </summary>
        public static readonly ValueConverter<BrowserProfileId, Guid> BrowserProfileId = new(
            id => id.Value,
            value => Domain.Common.BrowserProfileId.From(value)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.ExternalJobId"/> as text.
        /// </summary>
        public static readonly ValueConverter<ExternalJobId, string> ExternalJobId = new(
            id => id.Value,
            value => Domain.Sourcing.ExternalJobId.Create(value)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.JobTitle"/> as text.
        /// </summary>
        public static readonly ValueConverter<JobTitle, string> JobTitle = new(
            title => title.Value,
            value => Domain.Sourcing.JobTitle.Create(value)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.CompanyName"/> as text.
        /// </summary>
        public static readonly ValueConverter<CompanyName?, string> CompanyName = new(
            company => company!.Value,
            value => Domain.Sourcing.CompanyName.Create(value)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.CanonicalUrl"/> as text; the source is implied by the host.
        /// </summary>
        public static readonly ValueConverter<CanonicalUrl, string> CanonicalUrl = new(
            url => url.Value,
            value => Domain.Sourcing.CanonicalUrl.Parse(value)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.ContentHash"/> as 64 hex characters.
        /// </summary>
        public static readonly ValueConverter<ContentHash, string> ContentHash = new(
            hash => hash.Value,
            value => Domain.Sourcing.ContentHash.Parse(value)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.SearchCriteria"/> as JSON.
        /// </summary>
        public static readonly ValueConverter<SearchCriteria, string> SearchCriteria = new(
            criteria => Json.WriteCriteria(criteria),
            json => Json.ReadCriteria(json)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.RunLimits"/> as JSON.
        /// </summary>
        public static readonly ValueConverter<RunLimits, string> RunLimits = new(
            limits => Json.WriteLimits(limits),
            json => Json.ReadLimits(json)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.RunStats"/> as JSON.
        /// </summary>
        public static readonly ValueConverter<RunStats, string> RunStats = new(
            stats => Json.WriteStats(stats),
            json => Json.ReadStats(json)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.JobLocation"/> as JSON.
        /// </summary>
        public static readonly ValueConverter<JobLocation?, string> JobLocation = new(
            location => Json.WriteLocation(location!),
            json => Json.ReadLocation(json)
        );

        /// <summary>
        /// <see cref="Domain.Sourcing.SalaryRange"/> as JSON.
        /// </summary>
        public static readonly ValueConverter<SalaryRange?, string> SalaryRange = new(
            salary => Json.WriteSalary(salary!),
            json => Json.ReadSalary(json)
        );

        /// <summary>
        /// A posting's skills as a JSON array of display names.
        /// </summary>
        public static readonly ValueConverter<IReadOnlyList<JobSkill>, string> Skills = new(
            skills => Json.WriteSkills(skills),
            json => Json.ReadSkills(json)
        );

        /// <summary>
        /// A string list as a JSON array, for small resume-style state.
        /// </summary>
        public static readonly ValueConverter<IReadOnlyList<string>, string> StringList = new(
            ids => Json.WriteStringList(ids),
            json => Json.ReadStringList(json)
        );

        /// <summary>
        /// <see cref="Domain.Identity.UserId"/> as a GUID.
        /// </summary>
        public static readonly ValueConverter<UserId, Guid> UserId = new(
            id => id.Value,
            value => Domain.Identity.UserId.From(value)
        );

        /// <summary>
        /// <see cref="Domain.Identity.RoleId"/> as a GUID.
        /// </summary>
        public static readonly ValueConverter<RoleId, Guid> RoleId = new(
            id => id.Value,
            value => Domain.Identity.RoleId.From(value)
        );

        /// <summary>
        /// <see cref="Domain.Identity.PasswordHash"/> as
        /// <c>iterations:salt:hash</c> with base64 parts. Reads go back
        /// through <c>Restore</c> so a corrupt row fails loudly on load.
        /// </summary>
        public static readonly ValueConverter<PasswordHash, string> PasswordHash = new(
            hash => WritePasswordHash(hash),
            value => ReadPasswordHash(value)
        );

        /// <summary>
        /// A user's assigned roles as a JSON array of GUIDs. The aggregate
        /// mutates the list in place, so the comparer snapshots by value.
        /// </summary>
        public static readonly ValueConverter<List<RoleId>, string> RoleIdList = new(
            ids => Json.WriteGuidList(ids.Select(id => id.Value).ToArray()),
            json => Json.ReadGuidList(json).Select(Domain.Identity.RoleId.From).ToList()
        );

        /// <summary>
        /// Detects role-assignment changes by value, not by list reference.
        /// </summary>
        public static readonly ValueComparer<List<RoleId>> RoleIdListComparer = new(
            (left, right) =>
                left == null
                    ? right == null
                    : right != null
                        && left.Select(id => id.Value).SequenceEqual(right.Select(id => id.Value)),
            ids => ids.Aggregate(0, (hash, id) => HashCode.Combine(hash, id.Value.GetHashCode())),
            ids => ids.ToList()
        );

        /// <summary>
        /// A role's granted permissions as a JSON array of key strings. The
        /// aggregate mutates the list in place, so the comparer snapshots by
        /// value.
        /// </summary>
        public static readonly ValueConverter<List<PermissionKey>, string> PermissionList = new(
            keys => Json.WriteStringList(keys.Select(key => key.Value).ToArray()),
            // Syntactically invalid stored keys throw at load time via
            // PermissionKey.From, so only catalog-unknown keys reach LoginHandler.
            json => Json.ReadStringList(json).Select(Domain.Identity.PermissionKey.From).ToList()
        );

        /// <summary>
        /// Detects permission-grant changes by value, not by list reference.
        /// </summary>
        public static readonly ValueComparer<List<PermissionKey>> PermissionListComparer = new(
            (left, right) =>
                left == null
                    ? right == null
                    : right != null
                        && left.Select(key => key.Value)
                            .SequenceEqual(right.Select(key => key.Value)),
            keys =>
                keys.Aggregate(
                    0,
                    (hash, key) =>
                        HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(key.Value))
                ),
            keys => keys.ToList()
        );

        /// <summary>
        /// Detects skill changes by normalized name and order, not by list reference.
        /// </summary>
        public static readonly ValueComparer<IReadOnlyList<JobSkill>> SkillsComparer = new(
            (left, right) =>
                left == null
                    ? right == null
                    : right != null
                        && left.Select(skill => skill.NormalizedName)
                            .SequenceEqual(right.Select(skill => skill.NormalizedName)),
            skills =>
                skills.Aggregate(
                    0,
                    (hash, skill) =>
                        HashCode.Combine(
                            hash,
                            StringComparer.Ordinal.GetHashCode(skill.NormalizedName)
                        )
                ),
            skills => skills.ToArray()
        );

        /// <summary>
        /// Writes a password hash as <c>iterations:salt:hash</c>.
        /// </summary>
        /// <param name="hash">The hash.</param>
        /// <returns>The stored form.</returns>
        private static string WritePasswordHash(Domain.Identity.PasswordHash hash)
        {
            return string.Concat(
                hash.Iterations.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ":",
                Convert.ToBase64String(hash.Salt),
                ":",
                Convert.ToBase64String(hash.Hash)
            );
        }

        /// <summary>
        /// Reads a stored password hash back through the validating factory.
        /// </summary>
        /// <param name="value">The stored form.</param>
        /// <returns>The rehydrated hash.</returns>
        private static Domain.Identity.PasswordHash ReadPasswordHash(string value)
        {
            var parts = value.Split(':');
            if (
                parts.Length != 3
                || !int.TryParse(
                    parts[0],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var iterations
                )
            )
            {
                throw new System.Text.Json.JsonException("Stored password hash is malformed.");
            }

            return Domain.Identity.PasswordHash.Restore(
                Convert.FromBase64String(parts[1]),
                Convert.FromBase64String(parts[2]),
                iterations
            );
        }

        /// <summary>
        /// JSON shapes for multi-field value objects. Private DTOs keep the
        /// storage format independent of the domain types' shape.
        /// </summary>
        private static class Json
        {
            private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

            public static string WriteCriteria(SearchCriteria criteria)
            {
                return JsonSerializer.Serialize(
                    new CriteriaJson(
                        criteria.Keywords,
                        [.. criteria.Locations],
                        criteria.ExperienceYears,
                        criteria.PostedWithinDays,
                        criteria.RemoteOnly,
                        [.. criteria.RequiredSkills],
                        [.. criteria.Companies]
                    ),
                    Options
                );
            }

            public static SearchCriteria ReadCriteria(string json)
            {
                var dto = Read<CriteriaJson>(json);
                return new SearchCriteria(
                    dto.Keywords,
                    dto.Locations ?? [],
                    dto.ExperienceYears,
                    dto.PostedWithinDays,
                    dto.RemoteOnly,
                    dto.RequiredSkills ?? [],
                    dto.Companies ?? []
                );
            }

            public static string WriteLimits(RunLimits limits)
            {
                return JsonSerializer.Serialize(
                    new LimitsJson(
                        limits.MaxPages,
                        limits.MaxJobs,
                        limits.MaxDetails,
                        limits.MinDelay,
                        limits.MaxDelay,
                        limits.StartPage
                    ),
                    Options
                );
            }

            public static RunLimits ReadLimits(string json)
            {
                var dto = Read<LimitsJson>(json);
                return new RunLimits(
                    dto.MaxPages,
                    dto.MaxJobs,
                    dto.MaxDetails,
                    dto.MinDelay,
                    dto.MaxDelay,
                    // Rows written before the page window existed carry no
                    // start page; they always began at page one. Anything
                    // outside 1..ceiling never passed validation, so a
                    // hand-edited row falls back instead of failing the read.
                    dto.StartPage
                        is int start
                    && start >= 1
                    && start <= global::JobScrape.Domain.Sourcing.RunLimits.MaxPagesCeiling
                        ? start
                        : 1
                );
            }

            public static string WriteStats(RunStats stats)
            {
                return JsonSerializer.Serialize(
                    new StatsJson(
                        stats.PagesFetched,
                        stats.PostingsFound,
                        stats.PostingsNew,
                        stats.PostingsUpdated,
                        stats.DetailsFetched,
                        stats.Challenges,
                        stats.Failures,
                        stats.PostedDateMismatches
                    ),
                    Options
                );
            }

            public static RunStats ReadStats(string json)
            {
                var dto = Read<StatsJson>(json);
                return Domain.Sourcing.RunStats.Restore(
                    dto.PagesFetched,
                    dto.PostingsFound,
                    dto.PostingsNew,
                    dto.PostingsUpdated,
                    dto.DetailsFetched,
                    dto.Challenges,
                    dto.Failures,
                    dto.PostedDateMismatches
                );
            }

            public static string WriteLocation(JobLocation location)
            {
                return JsonSerializer.Serialize(
                    new LocationJson(
                        location.Raw,
                        location.City,
                        location.Region,
                        location.Country,
                        location.WorkMode
                    ),
                    Options
                );
            }

            public static JobLocation ReadLocation(string json)
            {
                var dto = Read<LocationJson>(json);
                return new JobLocation(dto.Raw, dto.City, dto.Region, dto.Country, dto.WorkMode);
            }

            public static string WriteSalary(SalaryRange salary)
            {
                return JsonSerializer.Serialize(
                    new SalaryJson(salary.Min, salary.Max, salary.Currency, salary.Period),
                    Options
                );
            }

            public static SalaryRange ReadSalary(string json)
            {
                var dto = Read<SalaryJson>(json);
                return new SalaryRange(dto.Min, dto.Max, dto.Currency, dto.Period);
            }

            public static string WriteSkills(IReadOnlyList<JobSkill> skills)
            {
                return JsonSerializer.Serialize(
                    skills.Select(skill => skill.Name).ToArray(),
                    Options
                );
            }

            public static JobSkill[] ReadSkills(string json)
            {
                return Read<string[]>(json).Select(JobSkill.Create).ToArray();
            }

            public static string WriteStringList(IReadOnlyList<string> ids)
            {
                return JsonSerializer.Serialize(ids.ToArray(), Options);
            }

            public static string[] ReadStringList(string json)
            {
                return Read<string[]>(json);
            }

            public static string WriteGuidList(Guid[] ids)
            {
                return JsonSerializer.Serialize(
                    ids.Select(id => id.ToString("D")).ToArray(),
                    Options
                );
            }

            public static Guid[] ReadGuidList(string json)
            {
                return Read<string[]>(json).Select(Guid.Parse).ToArray();
            }

            private static T Read<T>(string json)
            {
                return JsonSerializer.Deserialize<T>(json, Options)
                    ?? throw new JsonException($"Stored {typeof(T).Name} is null.");
            }

            private sealed record CriteriaJson(
                string Keywords,
                string[]? Locations,
                int? ExperienceYears,
                int? PostedWithinDays,
                bool RemoteOnly,
                string[]? RequiredSkills,
                string[]? Companies
            );

            private sealed record LimitsJson(
                int MaxPages,
                int MaxJobs,
                int MaxDetails,
                TimeSpan MinDelay,
                TimeSpan MaxDelay,
                int? StartPage
            );

            private sealed record StatsJson(
                int PagesFetched,
                int PostingsFound,
                int PostingsNew,
                int PostingsUpdated,
                int DetailsFetched,
                int Challenges,
                int Failures,
                int PostedDateMismatches
            );

            private sealed record LocationJson(
                string Raw,
                string? City,
                string? Region,
                string? Country,
                WorkMode WorkMode
            );

            private sealed record SalaryJson(
                decimal? Min,
                decimal? Max,
                string Currency,
                SalaryPeriod Period
            );
        }
    }
}
