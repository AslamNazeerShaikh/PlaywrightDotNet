using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Creates a search profile. Every field is validated here, at the
    /// request boundary, and reported by its JSON name, so the domain only
    /// ever sees valid input. Caps default to the source's policy limits and
    /// may only be tightened: fewer pages, jobs, or details and longer delays
    /// (docs/12-web-cockpit.md §14.5).
    /// </summary>
    public sealed class CreateSearchProfileHandler
    {
        /// <summary>
        /// Maximum profile-name length.
        /// </summary>
        public const int MaxNameLength = 200;

        // Domain upper bounds the source defaults never exceed.
        private const int MaxMinDelayMs = 60_000;
        private const int MaxMaxDelayMs = 120_000;

        // All injected and never mutated.
        private readonly ISearchProfileRepository _profiles;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SourceSettingsCatalog _sources;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSearchProfileHandler"/> class.
        /// </summary>
        /// <param name="profiles">The profile repository.</param>
        /// <param name="unitOfWork">Commits the new profile.</param>
        /// <param name="sources">The configured sources and their policy limits.</param>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public CreateSearchProfileHandler(
            ISearchProfileRepository profiles,
            IUnitOfWork unitOfWork,
            SourceSettingsCatalog sources,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(clock);
            _profiles = profiles;
            _unitOfWork = unitOfWork;
            _sources = sources;
            _clock = clock;
        }

        /// <summary>
        /// Validates and saves a new profile.
        /// </summary>
        /// <param name="request">The request. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the saved profile; <c>VALIDATION_FAILED</c> with
        /// field errors; or <c>PROFILE_NAME_TAKEN</c> when the name is in use.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<Result<SearchProfileResponse>> HandleAsync(
            CreateSearchProfileRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            var errors = new Dictionary<string, string[]>();
            var name = Text.Collapse(request.Name);
            if (name.Length is 0 or > MaxNameLength)
            {
                errors["name"] = [$"A name of 1 to {MaxNameLength} characters is required."];
            }

            SourceSettings? settings = null;
            if (!SourceKey.TryParse(request.SourceKey, out var source))
            {
                errors["sourceKey"] = ["Choose naukri, indeed, or linkedin."];
            }
            else
            {
                settings = _sources.Find(source);
                if (settings is null)
                {
                    errors["sourceKey"] = [$"Source '{source}' is not configured."];
                }
            }

            ValidateCriteria(request, errors);
            if (settings is not null)
            {
                ValidateLimits(request, settings, errors);
            }

            if (errors.Count > 0)
            {
                return Result.Fail<SearchProfileResponse>(
                    Errors.ValidationFailed("The request contains invalid fields.", errors)
                );
            }

            if (await _profiles.NameExistsAsync(name, cancellationToken))
            {
                return Result.Fail<SearchProfileResponse>(Errors.ProfileNameTaken(name));
            }

            // Every rule above mirrors a domain invariant, so construction
            // cannot throw here; if it does, that is a bug and surfaces as a 500.
            var defaults = settings!.DefaultLimits;
            var profile = SearchProfile.Create(
                name,
                source!,
                new SearchCriteria(
                    request.Keywords,
                    request.Locations ?? [],
                    request.ExperienceYears,
                    request.PostedWithinDays,
                    request.RemoteOnly,
                    request.RequiredSkills ?? []
                ),
                new RunLimits(
                    request.MaxPages ?? defaults.MaxPages,
                    request.MaxJobs ?? defaults.MaxJobs,
                    request.MaxDetails ?? defaults.MaxDetails,
                    request.MinDelayMs is { } min
                        ? TimeSpan.FromMilliseconds(min)
                        : defaults.MinDelay,
                    request.MaxDelayMs is { } max
                        ? TimeSpan.FromMilliseconds(max)
                        : defaults.MaxDelay
                ),
                _clock.GetUtcNow()
            );
            await _profiles.AddAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(SearchProfileResponse.From(profile));
        }

        /// <summary>
        /// Checks keywords, locations, and filters against the
        /// <see cref="SearchCriteria"/> invariants.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="errors">Field errors to add to.</param>
        private static void ValidateCriteria(
            CreateSearchProfileRequest request,
            Dictionary<string, string[]> errors
        )
        {
            var keywords = Text.Collapse(request.Keywords);
            if (keywords.Length is 0 or > SearchCriteria.MaxKeywordsLength)
            {
                errors["keywords"] =
                [
                    $"Keywords of 1 to {SearchCriteria.MaxKeywordsLength} characters are required.",
                ];
            }

            var locations = (request.Locations ?? [])
                .Select(Text.Collapse)
                .Where(location => location.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (locations > SearchCriteria.MaxLocations)
            {
                errors["locations"] =
                [
                    $"At most {SearchCriteria.MaxLocations} locations are allowed.",
                ];
            }
            else if (locations == 0 && !request.RemoteOnly)
            {
                errors["locations"] = ["Add at least one location, or search remote only."];
            }

            if (request.ExperienceYears is < 0 or > 50)
            {
                errors["experienceYears"] = ["Must be between 0 and 50."];
            }

            if (request.PostedWithinDays is < 1 or > 30)
            {
                errors["postedWithinDays"] = ["Must be between 1 and 30."];
            }

            var requiredSkills = (request.RequiredSkills ?? [])
                .Select(Text.Collapse)
                .Where(skill => skill.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (requiredSkills > SearchCriteria.MaxRequiredSkills)
            {
                errors["requiredSkills"] =
                [
                    $"At most {SearchCriteria.MaxRequiredSkills} required skills are allowed.",
                ];
            }
        }

        /// <summary>
        /// Checks the requested caps against the source policy: counts may
        /// only go down, delays may only go up.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="settings">The source settings.</param>
        /// <param name="errors">Field errors to add to.</param>
        private static void ValidateLimits(
            CreateSearchProfileRequest request,
            SourceSettings settings,
            Dictionary<string, string[]> errors
        )
        {
            var defaults = settings.DefaultLimits;
            var policy = $"the {settings.Source} policy";

            if (request.MaxPages is < 1 || request.MaxPages > defaults.MaxPages)
            {
                errors["maxPages"] = [$"Must be between 1 and {defaults.MaxPages} ({policy})."];
            }

            if (request.MaxJobs is < 1 || request.MaxJobs > defaults.MaxJobs)
            {
                errors["maxJobs"] = [$"Must be between 1 and {defaults.MaxJobs} ({policy})."];
            }

            if (request.MaxDetails is < 0 || request.MaxDetails > defaults.MaxDetails)
            {
                errors["maxDetails"] = [$"Must be between 0 and {defaults.MaxDetails} ({policy})."];
            }

            var floorMin = (int)defaults.MinDelay.TotalMilliseconds;
            var minDelay = request.MinDelayMs ?? floorMin;
            if (minDelay < floorMin || minDelay > MaxMinDelayMs)
            {
                errors["minDelayMs"] =
                [
                    $"Must be between {floorMin} and {MaxMinDelayMs} ({policy}).",
                ];
            }

            var floorMax = Math.Max((int)defaults.MaxDelay.TotalMilliseconds, minDelay);
            if (
                request.MaxDelayMs is { } maxDelay
                && (maxDelay < floorMax || maxDelay > MaxMaxDelayMs)
            )
            {
                errors["maxDelayMs"] =
                [
                    $"Must be between {floorMax} and {MaxMaxDelayMs} and at least minDelayMs ({policy}).",
                ];
            }
            else if (request.MaxDelayMs is null && minDelay > defaults.MaxDelay.TotalMilliseconds)
            {
                errors["maxDelayMs"] = ["Set maxDelayMs at least as long as minDelayMs."];
            }
        }
    }
}
