using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Scraping;
using JobScrape.Application.Results;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;

namespace JobScrape.Application.Features.SearchProfiles
{
    /// <summary>
    /// Creates a search profile. <see cref="SearchProfileValidator"/> checks
    /// every field at the request boundary and reports it by JSON name, so
    /// the domain only ever sees valid input. Caps default to the source's
    /// policy limits and may only be tightened: fewer pages, jobs, or details
    /// and longer delays (docs/12-web-cockpit.md §14.5).
    /// </summary>
    public sealed class CreateSearchProfileHandler
    {
        private readonly ISearchProfileRepository _profiles;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SourceSettingsCatalog _sources;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Gets the calling actor. Stored for future authorization; the
        /// single local user is always permitted, so creates are not gated.
        /// </summary>
        public ICallerContext Caller { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSearchProfileHandler"/> class.
        /// </summary>
        /// <param name="profiles">The profile repository.</param>
        /// <param name="unitOfWork">Commits the new profile.</param>
        /// <param name="sources">The configured sources and their policy limits.</param>
        /// <param name="clock">The time source.</param>
        /// <param name="caller">The calling actor. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public CreateSearchProfileHandler(
            ISearchProfileRepository profiles,
            IUnitOfWork unitOfWork,
            SourceSettingsCatalog sources,
            TimeProvider clock,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(sources);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(caller);
            _profiles = profiles;
            _unitOfWork = unitOfWork;
            _sources = sources;
            _clock = clock;
            Caller = caller;
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
            var values = SearchProfileValidator.Validate(
                new SearchProfileInput(
                    request.Name,
                    request.SourceKey,
                    request.Keywords,
                    request.Locations,
                    request.ExperienceYears,
                    request.PostedWithinDays,
                    request.RemoteOnly,
                    request.MaxPages,
                    request.StartPage,
                    request.MaxJobs,
                    request.MaxDetails,
                    request.MinDelayMs,
                    request.MaxDelayMs,
                    request.RequiredSkills,
                    request.Companies
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

            if (await _profiles.NameExistsAsync(values.Name, cancellationToken))
            {
                return Result.Fail<SearchProfileResponse>(Errors.ProfileNameTaken(values.Name));
            }

            var profile = SearchProfile.Create(
                values.Name,
                values.Source,
                values.Criteria,
                values.Limits,
                _clock.GetUtcNow()
            );
            await _profiles.AddAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(SearchProfileResponse.From(profile));
        }
    }
}
