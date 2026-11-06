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
        /// Creates a conflict for a browser-profile rename that cannot move
        /// the user-data directory safely.
        /// </summary>
        /// <param name="sessionId">The profile being renamed.</param>
        /// <param name="reason">Operator-safe reason.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo SessionRenameBlocked(Guid sessionId, string reason)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return new ErrorInfo(
                "SESSION_RENAME_BLOCKED",
                "Browser profile rename blocked.",
                reason,
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["sessionId"] = sessionId }
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
        /// Creates a not-found failure for a missing database table.
        /// </summary>
        /// <param name="tableName">The requested table name, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo TableNotFound(string tableName)
        {
            ArgumentNullException.ThrowIfNull(tableName);
            return new ErrorInfo(
                "TABLE_NOT_FOUND",
                "Table not found.",
                $"Table '{tableName}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["tableName"] = tableName }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing database row.
        /// </summary>
        /// <param name="table">The table that was searched.</param>
        /// <param name="pk">The requested primary key value, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo TableRowNotFound(string table, string pk)
        {
            ArgumentNullException.ThrowIfNull(table);
            ArgumentNullException.ThrowIfNull(pk);
            return new ErrorInfo(
                "ROW_NOT_FOUND",
                "Row not found.",
                $"Row '{pk}' does not exist in table '{table}'.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["tableName"] = table, ["primaryKeyValue"] = pk }
            );
        }

        /// <summary>
        /// Creates a validation failure for a database table name the explorer
        /// rejects. Preserves the explorer's message verbatim.
        /// </summary>
        /// <param name="detail">Operator-safe detail from the explorer.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo InvalidTable(string detail)
        {
            ArgumentNullException.ThrowIfNull(detail);
            return new ErrorInfo(
                "INVALID_TABLE",
                "Invalid table.",
                detail,
                ErrorKind.Validation,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a validation failure for a database row write the explorer
        /// rejects. Preserves the explorer's message verbatim.
        /// </summary>
        /// <param name="detail">Operator-safe detail from the explorer.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo InvalidInput(string detail)
        {
            ArgumentNullException.ThrowIfNull(detail);
            return new ErrorInfo(
                "INVALID_INPUT",
                "Invalid input.",
                detail,
                ErrorKind.Validation,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a validation failure for a database row write SQLite
        /// refused. The driver message is never echoed: it can leak SQL.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo DatabaseError()
        {
            return new ErrorInfo(
                "DB_ERROR",
                "Database operation failed.",
                "The database operation failed.",
                ErrorKind.Validation,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a validation failure for a company-split export when the
        /// run has no company list.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo ExportCompaniesMissing()
        {
            return new ErrorInfo(
                "EXPORT_COMPANIES_MISSING",
                "Company filter is empty.",
                "Add company names to the profile before using this export.",
                ErrorKind.Validation,
                new Dictionary<string, object?>()
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
        /// Creates a validation failure for an export group no writer renders.
        /// </summary>
        /// <param name="group">The requested group, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo ExportGroupUnsupported(string group)
        {
            ArgumentNullException.ThrowIfNull(group);
            return new ErrorInfo(
                "EXPORT_GROUP_UNSUPPORTED",
                "Export group is not supported.",
                $"Export group '{group}' is not supported. Use 'all', 'only' or 'exclude'.",
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["group"] = group }
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

        /// <summary>
        /// Creates an unavailable failure for an idempotency reservation
        /// that is still pending and did not complete within the wait window.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Unavailable"/>.</returns>
        public static ErrorInfo IdempotencyPending()
        {
            return new ErrorInfo(
                "IDEMPOTENCY_IN_PROGRESS",
                "The request is already in progress.",
                "The request is already in progress.",
                ErrorKind.Unavailable,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates an authentication failure for a wrong email or password.
        /// The detail never says which one was wrong. Maps to HTTP 401, never
        /// 400: bad credentials are not a validation error.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Unauthorized"/>.</returns>
        public static ErrorInfo InvalidCredentials()
        {
            return new ErrorInfo(
                "INVALID_CREDENTIALS",
                "Invalid email or password.",
                "The email or password is incorrect.",
                ErrorKind.Unauthorized,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates an authentication failure for a request with no usable
        /// bearer token. Returned by the JWT challenge handler, never by
        /// domain or application code.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Unauthorized"/>.</returns>
        public static ErrorInfo Unauthenticated()
        {
            return new ErrorInfo(
                "UNAUTHENTICATED",
                "Authentication is required.",
                "Sign in and retry with a bearer token.",
                ErrorKind.Unauthorized,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates an authorization failure for an authenticated caller
        /// missing the required role or permission. Returned by the
        /// authorization handlers, never by domain or application code.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Forbidden"/>.</returns>
        public static ErrorInfo AccessDenied()
        {
            return new ErrorInfo(
                "ACCESS_DENIED",
                "Access is denied.",
                "The account lacks the required role or permission.",
                ErrorKind.Forbidden,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a rate-limit failure for HTTP throttling. Returned by
        /// the rate-limiter rejection handler, never by domain code.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.RateLimited"/>.</returns>
        public static ErrorInfo TooManyRequests()
        {
            return new ErrorInfo(
                "RATE_LIMITED",
                "Too many requests.",
                "The request rate is too high; retry later.",
                ErrorKind.RateLimited,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a forbidden failure for a login while the account is locked.
        /// </summary>
        /// <param name="lockedUntil">When the lockout ends, if known.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Forbidden"/>.</returns>
        public static ErrorInfo AccountLocked(DateTimeOffset? lockedUntil)
        {
            return new ErrorInfo(
                "ACCOUNT_LOCKED",
                "The account is locked.",
                "Too many failed sign-in attempts; try again later.",
                ErrorKind.Forbidden,
                new Dictionary<string, object?> { ["retryAfter"] = lockedUntil }
            );
        }

        /// <summary>
        /// Creates a conflict failure for a password setup on an account
        /// that already completed first login.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo PasswordSetupComplete()
        {
            return new ErrorInfo(
                "PASSWORD_SETUP_COMPLETE",
                "The password is already set.",
                "First login is already complete; use the change-password flow instead.",
                ErrorKind.Conflict,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a conflict failure for an idempotency key reused with a
        /// different request body. The first response is never replayed for
        /// a different request: the caller must send a fresh key.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo IdempotencyKeyReused()
        {
            return new ErrorInfo(
                "IDEMPOTENCY_KEY_REUSED",
                "Idempotency key was reused with a different request.",
                "The idempotency key was already used with a different request body; retry with a fresh key.",
                ErrorKind.Conflict,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing user.
        /// </summary>
        /// <param name="email">The requested email, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo UserNotFound(string email)
        {
            ArgumentNullException.ThrowIfNull(email);
            return new ErrorInfo(
                "USER_NOT_FOUND",
                "User not found.",
                $"User '{email}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["email"] = email }
            );
        }

        /// <summary>
        /// Creates a conflict failure for an email that is already taken.
        /// </summary>
        /// <param name="email">The requested email.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo EmailTaken(string email)
        {
            ArgumentNullException.ThrowIfNull(email);
            return new ErrorInfo(
                "EMAIL_TAKEN",
                "Email already in use.",
                $"A user with email '{email}' already exists.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["email"] = email }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing role.
        /// </summary>
        /// <param name="name">The requested role name, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo RoleNotFound(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new ErrorInfo(
                "ROLE_NOT_FOUND",
                "Role not found.",
                $"Role '{name}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["name"] = name }
            );
        }

        /// <summary>
        /// Creates a conflict failure for a role name that already exists.
        /// </summary>
        /// <param name="name">The requested role name.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo RoleNameTaken(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new ErrorInfo(
                "ROLE_NAME_TAKEN",
                "Role already exists.",
                $"A role named '{name}' already exists.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["name"] = name }
            );
        }

        /// <summary>
        /// Creates a conflict failure for assigning a role the user already has.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo RoleAlreadyAssigned(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            return new ErrorInfo(
                "ROLE_ALREADY_ASSIGNED",
                "Role already assigned.",
                $"The user already has role '{name}'.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["name"] = name }
            );
        }

        /// <summary>
        /// Creates a conflict failure for revoking the last admin assignment.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo LastAdminRequired()
        {
            return new ErrorInfo(
                "LAST_ADMIN_REQUIRED",
                "At least one admin is required.",
                "Assign the admin role to another user before revoking it here.",
                ErrorKind.Conflict,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a forbidden failure for stripping a permission from the
        /// admin role. The admin role keeps the full catalog by definition;
        /// use role assignment to limit an operator instead.
        /// </summary>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Forbidden"/>.</returns>
        public static ErrorInfo RoleProtected()
        {
            return new ErrorInfo(
                "ROLE_PROTECTED",
                "The admin role cannot lose permissions.",
                "The admin role keeps every catalog permission; assign a narrower role instead.",
                ErrorKind.Forbidden,
                new Dictionary<string, object?>()
            );
        }

        /// <summary>
        /// Creates a validation failure for a permission outside the catalog.
        /// </summary>
        /// <param name="key">The requested permission key, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo UnknownPermission(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return new ErrorInfo(
                "UNKNOWN_PERMISSION",
                "Unknown permission.",
                $"Permission '{key}' is not in the v1 catalog.",
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["key"] = key }
            );
        }

        /// <summary>
        /// Creates a conflict failure for granting a permission the role already has.
        /// </summary>
        /// <param name="key">The permission key.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Conflict"/>.</returns>
        public static ErrorInfo PermissionAlreadyGranted(string key)
        {
            ArgumentNullException.ThrowIfNull(key);
            return new ErrorInfo(
                "PERMISSION_ALREADY_GRANTED",
                "Permission already granted.",
                $"The role already has permission '{key}'.",
                ErrorKind.Conflict,
                new Dictionary<string, object?> { ["key"] = key }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a quota scope outside the
        /// <see cref="QuotaScope"/> enum.
        /// </summary>
        /// <param name="scope">The requested scope, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo UnknownQuotaScope(string? scope)
        {
            return new ErrorInfo(
                "UNKNOWN_QUOTA_SCOPE",
                "Unknown quota scope.",
                $"Quota scope '{scope}' is unknown. Use 'Api' or 'Mcp'.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["scope"] = scope }
            );
        }

        /// <summary>
        /// Creates a quota-exceeded failure for a per-user usage quota.
        /// Enforcement counters stay in memory at the edge; only the
        /// definition persists, so metadata carries the definition that
        /// applied, not a counter row.
        /// </summary>
        /// <param name="scope">The governed surface.</param>
        /// <param name="limit">The requests-per-window limit that applied.</param>
        /// <param name="window">The fixed window that applied.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.QuotaExceeded"/>.</returns>
        public static ErrorInfo UsageQuotaExceeded(QuotaScope scope, int limit, TimeSpan window)
        {
            return new ErrorInfo(
                "QUOTA_EXCEEDED",
                "Usage quota exceeded.",
                $"The {scope.ToString().ToLowerInvariant()} usage quota ({limit} requests per {window}) has been reached; retry when the window resets.",
                ErrorKind.QuotaExceeded,
                new Dictionary<string, object?>
                {
                    ["scope"] = scope.ToString(),
                    ["limit"] = limit,
                    ["window"] = window.ToString(),
                }
            );
        }
    }
}
