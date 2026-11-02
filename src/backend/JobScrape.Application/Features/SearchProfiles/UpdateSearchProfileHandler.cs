using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Replaces a stored search profile's editable fields. The source stays
    /// immutable, the name stays unique, and every field goes through the
    /// same validator as creation.
    /// </summary>
    public sealed class UpdateSearchProfileHandler
    {
        private readonly ISearchProfileRepository _profiles;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SourceSettingsCatalog _sources;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateSearchProfileHandler"/> class.
        /// </summary>
        /// <param name="profiles">The profile repository.</param>
        /// <param name="unitOfWork">Commits the changes.</param>
        /// <param name="sources">The configured sources and their policy limits.</param>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public UpdateSearchProfileHandler(
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
        /// Validates and saves a replacement for the stored profile.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="request">The replacement fields. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the saved profile; <c>VALIDATION_FAILED</c> with
        /// field errors; <c>PROFILE_NOT_FOUND</c> when absent; or
        /// <c>PROFILE_NAME_TAKEN</c> when the new name is in use.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<Result<SearchProfileResponse>> HandleAsync(
            Guid id,
            UpdateSearchProfileRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            if (id == Guid.Empty)
            {
                return Result.Fail<SearchProfileResponse>(
                    Errors.ValidationFailed(
                        "The request contains invalid fields.",
                        new Dictionary<string, string[]> { ["id"] = ["A profile id is required."] }
                    )
                );
            }

            var profile = await _profiles.GetAsync(SearchProfileId.From(id), cancellationToken);
            if (profile is null)
            {
                return Result.Fail<SearchProfileResponse>(Errors.ProfileNotFound(id));
            }

            var errors = new Dictionary<string, string[]>();
            var values = SearchProfileValidator.Validate(
                new SearchProfileInput(
                    request.Name,
                    profile.Source.Value,
                    request.Keywords,
                    request.Locations,
                    request.ExperienceYears,
                    request.PostedWithinDays,
                    request.RemoteOnly,
                    request.MaxPages,
                    request.MaxJobs,
                    request.MaxDetails,
                    request.MinDelayMs,
                    request.MaxDelayMs,
                    request.RequiredSkills
                ),
                _sources,
                errors
            );
            if (values is null)
            {
                return Result.Fail<SearchProfileResponse>(
                    Errors.ValidationFailed("The request contains invalid fields.", errors)
                );
            }

            var renamed = !string.Equals(values.Name, profile.Name, StringComparison.Ordinal);
            var caseOnly =
                renamed
                && string.Equals(values.Name, profile.Name, StringComparison.OrdinalIgnoreCase);
            if (
                renamed
                && !caseOnly
                && await _profiles.NameExistsAsync(values.Name, cancellationToken)
            )
            {
                return Result.Fail<SearchProfileResponse>(Errors.ProfileNameTaken(values.Name));
            }

            if (request.Enabled && profile.Source.Tier == ComplianceTier.HighRisk)
            {
                errors["enabled"] =
                [
                    "High-risk sources need interactive consent before they can be enabled.",
                ];
                return Result.Fail<SearchProfileResponse>(
                    Errors.ValidationFailed("The request contains invalid fields.", errors)
                );
            }

            var now = _clock.GetUtcNow();
            profile.Rename(values.Name, now);
            profile.UpdateCriteria(values.Criteria, now);
            profile.UpdateLimits(values.Limits, now);
            if (request.Enabled)
            {
                profile.Enable(highRiskConsentAcknowledged: false, now);
            }
            else
            {
                profile.Disable(now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(SearchProfileResponse.From(profile));
        }
    }
}
