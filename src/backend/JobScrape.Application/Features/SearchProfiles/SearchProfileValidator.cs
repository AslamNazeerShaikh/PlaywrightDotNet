using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Validates and normalizes one search-profile field set. Create and
    /// update both use it, so the API reports the same rules and the domain
    /// only ever sees valid values.
    /// </summary>
    public static class SearchProfileValidator
    {
        /// <summary>
        /// Maximum profile-name length.
        /// </summary>
        public const int MaxNameLength = 200;

        // Hard upper bounds for the delay window.
        private const int MaxMinDelayMs = 60_000;
        private const int MaxMaxDelayMs = 120_000;

        /// <summary>
        /// Validates the input against the source policy.
        /// </summary>
        /// <param name="input">The merged field set. Must not be null.</param>
        /// <param name="sources">The configured sources. Must not be null.</param>
        /// <param name="errors">Field errors keyed by JSON request name.</param>
        /// <returns>The normalized values, or null when any field is invalid.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public static SearchProfileValues? Validate(
            SearchProfileInput input,
            SourceSettingsCatalog sources,
            Dictionary<string, string[]> errors
        )
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(errors);

            var name = Text.Collapse(input.Name);
            if (name.Length is 0 or > MaxNameLength)
            {
                errors["name"] = [$"A name of 1 to {MaxNameLength} characters is required."];
            }

            SourceSettings? settings = null;
            if (!SourceKey.TryParse(input.SourceKey, out var source))
            {
                errors["sourceKey"] = ["Choose naukri, indeed, or linkedin."];
            }
            else
            {
                settings = sources.Find(source);
                if (settings is null)
                {
                    errors["sourceKey"] = [$"Source '{source}' is not configured."];
                }
            }

            ValidateCriteria(input, errors);
            if (settings is not null)
            {
                ValidateLimits(input, settings, errors);
            }

            if (errors.Count > 0)
            {
                return null;
            }

            var defaults = settings!.DefaultLimits;
            return new SearchProfileValues(
                name,
                source!,
                new SearchCriteria(
                    input.Keywords,
                    input.Locations ?? [],
                    input.ExperienceYears,
                    input.PostedWithinDays,
                    input.RemoteOnly,
                    input.RequiredSkills ?? [],
                    input.Companies ?? []
                ),
                new RunLimits(
                    input.MaxPages ?? defaults.MaxPages,
                    input.MaxJobs ?? defaults.MaxJobs,
                    input.MaxDetails ?? defaults.MaxDetails,
                    input.MinDelayMs is { } min
                        ? TimeSpan.FromMilliseconds(min)
                        : defaults.MinDelay,
                    input.MaxDelayMs is { } max
                        ? TimeSpan.FromMilliseconds(max)
                        : defaults.MaxDelay,
                    input.StartPage ?? 1
                )
            );
        }

        /// <summary>
        /// Checks keywords, locations, and filters against the
        /// <see cref="SearchCriteria"/> invariants.
        /// </summary>
        /// <param name="input">The field set.</param>
        /// <param name="errors">Field errors to add to.</param>
        private static void ValidateCriteria(
            SearchProfileInput input,
            Dictionary<string, string[]> errors
        )
        {
            var keywords = Text.Collapse(input.Keywords);
            if (keywords.Length is 0 or > SearchCriteria.MaxKeywordsLength)
            {
                errors["keywords"] =
                [
                    $"Keywords of 1 to {SearchCriteria.MaxKeywordsLength} characters are required.",
                ];
            }

            var locations = (input.Locations ?? [])
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
            else if (locations == 0 && !input.RemoteOnly)
            {
                errors["locations"] = ["Add at least one location, or search remote only."];
            }

            if (input.ExperienceYears is < 0 or > 50)
            {
                errors["experienceYears"] = ["Must be between 0 and 50."];
            }

            if (input.PostedWithinDays is < 1 or > 30)
            {
                errors["postedWithinDays"] = ["Must be between 1 and 30."];
            }

            var requiredSkills = (input.RequiredSkills ?? [])
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

            var companies = (input.Companies ?? [])
                .Select(Text.Collapse)
                .Where(company => company.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            if (companies > SearchCriteria.MaxCompanies)
            {
                errors["companies"] =
                [
                    $"At most {SearchCriteria.MaxCompanies} companies are allowed.",
                ];
            }
            else if (
                (input.Companies ?? [])
                    .Select(Text.Collapse)
                    .Any(company => company.Length > CompanyName.MaxLength)
            )
            {
                errors["companies"] =
                [
                    $"Each company name must be at most {CompanyName.MaxLength} characters.",
                ];
            }
        }

        /// <summary>
        /// Checks the requested caps against the source policy: counts must
        /// stay within the source ceiling, delays at or above the source
        /// minimum.
        /// </summary>
        /// <param name="input">The field set.</param>
        /// <param name="settings">The source settings.</param>
        /// <param name="errors">Field errors to add to.</param>
        private static void ValidateLimits(
            SearchProfileInput input,
            SourceSettings settings,
            Dictionary<string, string[]> errors
        )
        {
            var defaults = settings.DefaultLimits;
            var policy = $"the {settings.Source} policy";

            if (input.MaxPages is < 1 || input.MaxPages > settings.MaxPagesCeiling)
            {
                errors["maxPages"] =
                [
                    $"Must be between 1 and {settings.MaxPagesCeiling} ({policy}).",
                ];
            }

            var startPage = input.StartPage ?? 1;
            if (input.StartPage is < 1 || input.StartPage > settings.MaxPagesCeiling)
            {
                errors["startPage"] =
                [
                    $"Must be between 1 and {settings.MaxPagesCeiling} ({policy}).",
                ];
            }
            else
            {
                // A missing cap still fills from the source default, so the
                // check runs against the effective window, never raw input.
                // An invalid maxPages already owns its error; it gets no
                // second one here.
                var maxPagesValid =
                    input.MaxPages is null
                    || (input.MaxPages >= 1 && input.MaxPages <= settings.MaxPagesCeiling);
                var effectiveMax = input.MaxPages ?? defaults.MaxPages;
                if (maxPagesValid && startPage + effectiveMax - 1 > settings.MaxPagesCeiling)
                {
                    errors["startPage"] =
                    [
                        $"Pages {startPage}..{startPage + effectiveMax - 1} exceed the ceiling of {settings.MaxPagesCeiling} ({policy}).",
                    ];
                }
            }

            if (input.MaxJobs is < 1 || input.MaxJobs > settings.MaxJobsCeiling)
            {
                errors["maxJobs"] =
                [
                    $"Must be between 1 and {settings.MaxJobsCeiling} ({policy}).",
                ];
            }

            if (input.MaxDetails is < 0 || input.MaxDetails > defaults.MaxDetails)
            {
                errors["maxDetails"] = [$"Must be between 0 and {defaults.MaxDetails} ({policy})."];
            }

            var floorMin = (int)defaults.MinDelay.TotalMilliseconds;
            var minDelay = input.MinDelayMs ?? floorMin;
            if (minDelay < floorMin || minDelay > MaxMinDelayMs)
            {
                errors["minDelayMs"] =
                [
                    $"Must be between {floorMin} and {MaxMinDelayMs} ({policy}).",
                ];
            }

            var floorMax = Math.Max((int)defaults.MaxDelay.TotalMilliseconds, minDelay);
            if (
                input.MaxDelayMs is { } maxDelay
                && (maxDelay < floorMax || maxDelay > MaxMaxDelayMs)
            )
            {
                errors["maxDelayMs"] =
                [
                    $"Must be between {floorMax} and {MaxMaxDelayMs} and at least minDelayMs ({policy}).",
                ];
            }
            else if (input.MaxDelayMs is null && minDelay > defaults.MaxDelay.TotalMilliseconds)
            {
                errors["maxDelayMs"] = ["Set maxDelayMs at least as long as minDelayMs."];
            }
        }
    }
}
