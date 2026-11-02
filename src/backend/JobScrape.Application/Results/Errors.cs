using JobScrape.Domain.Access;
using JobScrape.Domain.Common;

namespace JobScrape.Application.Results
{
    /// <summary>
    /// Stable <see cref="ErrorInfo"/> factories. Codes are the UI contract:
    /// uppercase snake case, never localized, never repurposed once shipped.
    /// </summary>
    public static class Errors
    {
        /// <summary>
        /// Creates a validation failure carrying field-level errors.
        /// </summary>
        /// <param name="detail">Operator-safe detail describing the request problem.</param>
        /// <param name="errors">Field errors keyed by JSON request field name.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo ValidationFailed(
            string detail,
            IReadOnlyDictionary<string, string[]> errors
        )
        {
            ArgumentNullException.ThrowIfNull(detail);
            ArgumentNullException.ThrowIfNull(errors);
            return new ErrorInfo(
                "VALIDATION_FAILED",
                "One or more validation errors occurred.",
                detail,
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["errors"] = errors }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing search profile.
        /// </summary>
        /// <param name="profileId">The requested profile identifier, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo ProfileNotFound(Guid profileId)
        {
            return new ErrorInfo(
                "PROFILE_NOT_FOUND",
                "Search profile not found.",
                $"Search profile '{profileId}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["profileId"] = profileId }
            );
        }

        /// <summary>
        /// Creates a conflict for a profile name that is already in use.
        /// </summary>
        /// <param name="name">The requested name.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo ProfileNameTaken(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new ErrorInfo(
                "PROFILE_NAME_TAKEN",
                "Profile name already in use.",
                $"A search profile named '{name}' already exists.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["name"] = name }
            );
        }

        /// <summary>
        /// Creates a conflict for a browser profile name that is already in use.
        /// </summary>
        /// <param name="name">The requested name.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo SessionNameTaken(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new ErrorInfo(
                "SESSION_NAME_TAKEN",
                "Browser profile name already in use.",
                $"A browser profile named '{name}' already exists.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["name"] = name }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing browser profile session.
        /// </summary>
        /// <param name="sessionId">The requested profile identifier.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo SessionNotFound(Guid sessionId)
        {
            return new ErrorInfo(
                "SESSION_NOT_FOUND",
                "Browser profile not found.",
                $"Browser profile '{sessionId}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["sessionId"] = sessionId }
            );
        }

        /// <summary>
        /// Creates a conflict for a run requested from a disabled profile.
        /// </summary>
        /// <param name="profileId">The disabled profile.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo ProfileDisabled(Guid profileId)
        {
            return new ErrorInfo(
                "PROFILE_DISABLED",
                "Search profile is disabled.",
                $"Search profile '{profileId}' is disabled; enable it before running.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["profileId"] = profileId }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing browser profile.
        /// </summary>
        /// <param name="browserProfileId">The requested browser profile.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo BrowserProfileNotFound(Guid browserProfileId)
        {
            return new ErrorInfo(
                "BROWSER_PROFILE_NOT_FOUND",
                "Browser profile not found.",
                $"Browser profile '{browserProfileId}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["browserProfileId"] = browserProfileId }
            );
        }

        /// <summary>
        /// Creates a failure for a source with no adapter or no configuration.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Unavailable"/>.</returns>
        public static ErrorInfo SourceUnavailable(SourceKey source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new ErrorInfo(
                "SOURCE_UNAVAILABLE",
                "Source unavailable.",
                $"Source '{source}' has no configured adapter.",
                ErrorKind.Unavailable,
                new Dictionary<string, object?> { ["source"] = source.Value }
            );
        }

        /// <summary>
        /// Creates a failure for a source disabled in configuration.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Forbidden"/>.</returns>
        public static ErrorInfo SourceDisabled(SourceKey source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new ErrorInfo(
                "SOURCE_DISABLED",
                "Source disabled.",
                $"Source '{source}' is disabled in configuration.",
                ErrorKind.Forbidden,
                new Dictionary<string, object?> { ["source"] = source.Value }
            );
        }

        /// <summary>
        /// Creates a failure for a run the compliance gate refused.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="reason">Operator guidance naming the missing consent.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Forbidden"/>.</returns>
        public static ErrorInfo ComplianceBlocked(SourceKey source, string reason)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(reason);
            return new ErrorInfo(
                "COMPLIANCE_BLOCKED",
                "Compliance gate blocked the run.",
                reason,
                ErrorKind.Forbidden,
                new Dictionary<string, object?>
                {
                    ["source"] = source.Value,
                    ["tier"] = source.Tier.ToString(),
                }
            );
        }

        /// <summary>
        /// Creates a failure for an exhausted daily quota.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="decision">The refused quota decision.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.QuotaExceeded"/>.</returns>
        public static ErrorInfo QuotaExceeded(SourceKey source, QuotaDecision decision)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(decision);
            return new ErrorInfo(
                "QUOTA_EXCEEDED",
                "Source quota exceeded.",
                $"The daily {decision.Kind.ToString().ToLowerInvariant()} cap ({decision.Limit}) for source '{source}' has been reached.",
                ErrorKind.QuotaExceeded,
                new Dictionary<string, object?>
                {
                    ["source"] = source.Value,
                    ["quota"] = decision.Kind.ToString(),
                    ["limit"] = decision.Limit,
                    ["used"] = decision.Used,
                }
            );
        }

        /// <summary>
        /// Creates a failure for a source whose circuit breaker is open.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="openUntil">When a probe may be attempted, if known.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.CoolingDown"/>.</returns>
        public static ErrorInfo SourceCoolingDown(SourceKey source, DateTimeOffset? openUntil)
        {
            ArgumentNullException.ThrowIfNull(source);
            return new ErrorInfo(
                "SOURCE_COOLING_DOWN",
                "Source is cooling down.",
                $"Source '{source}' is blocked after repeated walls; wait for the cooldown.",
                ErrorKind.CoolingDown,
                new Dictionary<string, object?>
                {
                    ["source"] = source.Value,
                    ["retryAfter"] = openUntil,
                }
            );
        }

        /// <summary>
        /// Creates a failure for a browser profile that cannot launch now.
        /// </summary>
        /// <param name="browserProfileId">The profile.</param>
        /// <param name="resetRequired">True when the profile needs a reset rather than time.</param>
        /// <param name="cooldownUntil">When the cooldown ends, if cooling down.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.CoolingDown"/>.</returns>
        public static ErrorInfo SessionUnavailable(
            Guid browserProfileId,
            bool resetRequired,
            DateTimeOffset? cooldownUntil
        )
        {
            return new ErrorInfo(
                resetRequired ? "SESSION_RESET_REQUIRED" : "SESSION_COOLING_DOWN",
                resetRequired
                    ? "Browser profile needs a reset."
                    : "Browser profile is cooling down.",
                resetRequired
                    ? $"Browser profile '{browserProfileId}' hit too many walls; reset it."
                    : $"Browser profile '{browserProfileId}' is cooling down after a wall.",
                ErrorKind.CoolingDown,
                new Dictionary<string, object?>
                {
                    ["browserProfileId"] = browserProfileId,
                    ["retryAfter"] = cooldownUntil,
                }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing run.
        /// </summary>
        /// <param name="runId">The requested run identifier.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo RunNotFound(Guid runId)
        {
            return new ErrorInfo(
                "RUN_NOT_FOUND",
                "Run not found.",
                $"Run '{runId}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["runId"] = runId }
            );
        }

        /// <summary>
        /// Creates a validation failure for an export format no writer renders.
        /// </summary>
        /// <param name="format">The requested format, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo ExportFormatUnsupported(string format)
        {
            ArgumentNullException.ThrowIfNull(format);
            return new ErrorInfo(
                "EXPORT_FORMAT_UNSUPPORTED",
                "Export format is not supported.",
                $"Export format '{format}' is not supported. Use 'csv' or 'xlsx'.",
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["format"] = format }
            );
        }

        /// <summary>
        /// Creates a conflict failure when a run is already executing. Runs
        /// execute one at a time: a browser profile holds a single
        /// persistent context, and sources are paced per process.
        /// </summary>
        /// <param name="activeRunId">The executing run, once recorded.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo RunInProgress(Guid? activeRunId)
        {
            return new ErrorInfo(
                "RUN_IN_PROGRESS",
                "A run is already in progress.",
                activeRunId is { } id
                    ? $"Run '{id}' is still executing; wait for it or cancel it."
                    : "Another run is starting; wait for it or cancel it.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["activeRunId"] = activeRunId }
            );
        }

        /// <summary>
        /// Creates an unavailable failure for a run cancelled before it was
        /// recorded, because the host is stopping.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Unavailable"/>.</returns>
        public static ErrorInfo RunNotStarted()
        {
            return new ErrorInfo(
                "RUN_NOT_STARTED",
                "The run did not start.",
                "The API is stopping; start the run again once it is back.",
                ErrorKind.Unavailable,
                new Dictionary<string, object?>()
            );
        }
    }
}
