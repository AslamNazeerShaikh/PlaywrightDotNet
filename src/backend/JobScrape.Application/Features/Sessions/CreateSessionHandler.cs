using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Creates a browser profile in the <c>New</c> state. Every field is
    /// validated at the request boundary with the domain's own rules
    /// (<see cref="BrowserProfile.IsValidName"/>, engine/channel pairing), so
    /// the aggregate only ever sees valid input. Logging in is a separate,
    /// manual step that needs the Playwright session manager.
    /// </summary>
    public sealed class CreateSessionHandler
    {
        // All injected and never mutated.
        private readonly IBrowserProfileRepository _profiles;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;

        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSessionHandler"/> class.
        /// </summary>
        /// <param name="profiles">The browser profile repository.</param>
        /// <param name="unitOfWork">Commits the new profile.</param>
        /// <param name="clock">The time source.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public CreateSessionHandler(
            IBrowserProfileRepository profiles,
            IUnitOfWork unitOfWork,
            TimeProvider clock
        )
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(clock);
            _profiles = profiles;
            _unitOfWork = unitOfWork;
            _clock = clock;
        }

        /// <summary>
        /// Validates and saves a new browser profile.
        /// </summary>
        /// <param name="request">The request. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the saved profile; <c>VALIDATION_FAILED</c> with field
        /// errors; or <c>SESSION_NAME_TAKEN</c> when the name is in use.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<Result<SessionResponse>> HandleAsync(
            CreateSessionRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            var errors = new Dictionary<string, string[]>();
            var name = request.Name?.Trim() ?? string.Empty;
            if (!BrowserProfile.IsValidName(name))
            {
                errors["name"] =
                [
                    $"Use 1 to {BrowserProfile.MaxNameLength} letters, digits, '.', '_', or '-', not starting with '.'.",
                ];
            }

            if (!SourceKey.TryParse(request.SourceKey, out var source))
            {
                errors["sourceKey"] = ["Choose naukri, indeed, or linkedin."];
            }

            var engine = BrowserEngine.Chromium;
            if (
                request.Engine is not null
                && !SessionValidation.TryParseEnum(request.Engine, out engine)
            )
            {
                errors["engine"] = ["Choose Chromium, Firefox, or WebKit."];
            }

            var channel = BrowserChannel.Bundled;
            if (
                request.Channel is not null
                && !SessionValidation.TryParseEnum(request.Channel, out channel)
            )
            {
                errors["channel"] = ["Choose Bundled, Chrome, or Edge."];
            }
            else if (channel != BrowserChannel.Bundled && engine != BrowserEngine.Chromium)
            {
                errors["channel"] = [$"The {channel} channel requires the Chromium engine."];
            }

            if (errors.Count > 0)
            {
                return Result.Fail<SessionResponse>(
                    Errors.ValidationFailed("The request contains invalid fields.", errors)
                );
            }

            if (await _profiles.NameExistsAsync(name, cancellationToken))
            {
                return Result.Fail<SessionResponse>(Errors.SessionNameTaken(name));
            }

            var profile = BrowserProfile.Create(name, source!, engine, channel);
            await _profiles.AddAsync(profile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Ok(SessionResponse.From(profile, _clock.GetUtcNow()));
        }

    }
}
