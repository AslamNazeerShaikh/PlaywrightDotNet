using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using Microsoft.Extensions.Logging;

namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Updates a browser profile's name, engine, and channel. The source is
    /// immutable. A rename moves the user-data directory through
    /// <see cref="IBrowserProfileWorkspace"/> before the database row changes.
    /// </summary>
    public sealed partial class UpdateSessionHandler
    {
        private readonly IBrowserProfileRepository _profiles;
        private readonly IBrowserProfileWorkspace _workspace;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _clock;
        private readonly ILogger<UpdateSessionHandler> _logger;

        /// <summary>
        /// Gets the calling actor. Stored for future authorization; the
        /// single local user is always permitted, so updates are not gated.
        /// </summary>
        public ICallerContext Caller { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateSessionHandler"/> class.
        /// </summary>
        /// <param name="profiles">The browser profile repository.</param>
        /// <param name="workspace">Moves the user-data directory on rename.</param>
        /// <param name="unitOfWork">Commits the changes.</param>
        /// <param name="clock">The time source.</param>
        /// <param name="logger">Logs a failed compensating move.</param>
        /// <param name="caller">The calling actor. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public UpdateSessionHandler(
            IBrowserProfileRepository profiles,
            IBrowserProfileWorkspace workspace,
            IUnitOfWork unitOfWork,
            TimeProvider clock,
            ILogger<UpdateSessionHandler> logger,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(profiles);
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(unitOfWork);
            ArgumentNullException.ThrowIfNull(clock);
            ArgumentNullException.ThrowIfNull(logger);
            ArgumentNullException.ThrowIfNull(caller);
            _profiles = profiles;
            _workspace = workspace;
            _unitOfWork = unitOfWork;
            _clock = clock;
            _logger = logger;
            Caller = caller;
        }

        /// <summary>
        /// Validates and saves the changes.
        /// </summary>
        /// <param name="id">The browser profile identifier.</param>
        /// <param name="request">The fields to change. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// Success with the saved profile; <c>VALIDATION_FAILED</c> with
        /// field errors; <c>SESSION_NOT_FOUND</c> when absent;
        /// <c>SESSION_NAME_TAKEN</c> when the new name is in use; or
        /// <c>SESSION_RENAME_BLOCKED</c> when the directory cannot move.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
        public async Task<Result<SessionResponse>> HandleAsync(
            Guid id,
            UpdateSessionRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(request);

            if (id == Guid.Empty)
            {
                return Result.Fail<SessionResponse>(
                    Errors.ValidationFailed(
                        "The request contains invalid fields.",
                        new Dictionary<string, string[]> { ["id"] = ["A session id is required."] }
                    )
                );
            }

            var profile = await _profiles.GetAsync(BrowserProfileId.From(id), cancellationToken);
            if (profile is null)
            {
                return Result.Fail<SessionResponse>(Errors.SessionNotFound(id));
            }

            var errors = new Dictionary<string, string[]>();
            var name = request.Name is null ? profile.Name : request.Name.Trim();
            if (!BrowserProfile.IsValidName(name))
            {
                errors["name"] =
                [
                    $"Use 1 to {BrowserProfile.MaxNameLength} letters, digits, '.', '_', or '-', not starting with '.'.",
                ];
            }

            var engine = profile.Engine;
            if (
                request.Engine is not null
                && !SessionValidation.TryParseEnum(request.Engine, out engine)
            )
            {
                errors["engine"] = ["Choose Chromium, Firefox, or WebKit."];
            }

            var channel = profile.Channel;
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

            var renamed = !string.Equals(name, profile.Name, StringComparison.Ordinal);
            var caseOnly =
                renamed && string.Equals(name, profile.Name, StringComparison.OrdinalIgnoreCase);
            if (renamed && !caseOnly && await _profiles.NameExistsAsync(name, cancellationToken))
            {
                return Result.Fail<SessionResponse>(Errors.SessionNameTaken(name));
            }

            var oldName = profile.Name;
            var moved = false;
            if (renamed)
            {
                var outcome = await _workspace.RenameAsync(profile, name, cancellationToken);
                if (outcome == ProfileDirectoryRename.ProfileInUse)
                {
                    return Result.Fail<SessionResponse>(
                        Errors.SessionRenameBlocked(
                            id,
                            "The browser profile is open in a running scrape. Stop the run and retry."
                        )
                    );
                }

                if (outcome == ProfileDirectoryRename.TargetExists)
                {
                    return Result.Fail<SessionResponse>(
                        Errors.SessionRenameBlocked(
                            id,
                            "A user-data directory with the new name already exists."
                        )
                    );
                }

                if (outcome == ProfileDirectoryRename.Failed)
                {
                    return Result.Fail<SessionResponse>(
                        Errors.SessionRenameBlocked(
                            id,
                            "The user-data directory could not be moved. Stop any process using it and retry."
                        )
                    );
                }

                moved =
                    outcome
                        is ProfileDirectoryRename.Moved
                            or ProfileDirectoryRename.AlreadyRenamed;
            }

            profile.Rename(name);
            profile.Configure(engine, channel);
            try
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                if (moved)
                {
                    try
                    {
                        var restore = await _workspace.RenameAsync(
                            profile,
                            oldName,
                            CancellationToken.None
                        );
                        if (
                            restore
                            is not (
                                ProfileDirectoryRename.Moved
                                or ProfileDirectoryRename.AlreadyRenamed
                                or ProfileDirectoryRename.NothingToMove
                            )
                        )
                        {
                            LogMoveBackIncomplete(_logger, id, oldName, restore);
                        }
                    }
                    catch (Exception moveBack)
                    {
                        LogMoveBackFailed(_logger, moveBack, id, oldName);
                    }

                    profile.Rename(oldName);
                }

                LogSaveFailed(_logger, exception, id);
                throw;
            }

            return Result.Ok(SessionResponse.From(profile, _clock.GetUtcNow()));
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Moving browser profile {ProfileId} back to {OldName} ended with {Outcome}."
        )]
        private static partial void LogMoveBackIncomplete(
            ILogger logger,
            Guid profileId,
            string oldName,
            ProfileDirectoryRename outcome
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Failed to move browser profile {ProfileId} back to {OldName} after a save failure."
        )]
        private static partial void LogMoveBackFailed(
            ILogger logger,
            Exception exception,
            Guid profileId,
            string oldName
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Saving browser profile {ProfileId} failed after a rename."
        )]
        private static partial void LogSaveFailed(
            ILogger logger,
            Exception exception,
            Guid profileId
        );
    }
}
