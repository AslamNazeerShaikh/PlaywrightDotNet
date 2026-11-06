using System.ComponentModel.DataAnnotations;
using JobScrape.Api.Http;
using JobScrape.Api.Http.Auth;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.Identity;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Sign-in, first-login setup, password change, and the current caller.
    /// Login and setup are anonymous; everything else needs a bearer token.
    /// Change-password and me stay on the global limiter only. Data-annotation failures map to coded validation
    /// problems below because the automatic 400 is suppressed in Program.
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    public sealed class AuthController : ControllerBase
    {
        // Constructor-injected handlers, never mutated.
        private readonly LoginHandler _login;
        private readonly SetupPasswordHandler _setup;
        private readonly ChangePasswordHandler _change;

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthController"/> class.
        /// </summary>
        /// <param name="login">Authenticates credentials.</param>
        /// <param name="setup">Completes first login.</param>
        /// <param name="change">Changes the password.</param>
        /// <exception cref="ArgumentNullException">Thrown when any handler is null.</exception>
        public AuthController(
            LoginHandler login,
            SetupPasswordHandler setup,
            ChangePasswordHandler change
        )
        {
            ArgumentNullException.ThrowIfNull(login);
            ArgumentNullException.ThrowIfNull(setup);
            ArgumentNullException.ThrowIfNull(change);
            _login = login;
            _setup = setup;
            _change = change;
        }

        /// <summary>
        /// Signs in with email and password.
        /// </summary>
        /// <param name="request">The credentials.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 with the token; 401 <c>INVALID_CREDENTIALS</c> for a wrong
        /// email or password or a pending first login; 403
        /// <c>ACCOUNT_LOCKED</c> while locked out.
        /// </returns>
        [HttpPost("login")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Login)]
        public async Task<IResult> LoginAsync(
            [FromBody] LoginRequest? request,
            CancellationToken cancellationToken
        )
        {
            if (request is null)
            {
                return Errors
                    .ValidationFailed(
                        "The request body is required.",
                        new Dictionary<string, string[]>
                        {
                            ["body"] = ["The request body is required."],
                        }
                    )
                    .ToProblem();
            }

            var invalid = ModelProblem();
            if (invalid is not null)
            {
                return invalid;
            }

            var result = await _login.HandleAsync(
                request!.Email,
                request.Password,
                cancellationToken
            );
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Completes first login by replacing the seeded password, then
        /// signs in with the new password.
        /// </summary>
        /// <param name="request">The account and passwords.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 with the token; 401 <c>INVALID_CREDENTIALS</c> for a wrong
        /// secret; 403 <c>ACCOUNT_LOCKED</c> when the account is locked out;
        /// 409 <c>PASSWORD_SETUP_COMPLETE</c> when first login
        /// already finished; 400 with field errors for a weak new password.
        /// </returns>
        [HttpPost("setup-password")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.Setup)]
        public async Task<IResult> SetupPasswordAsync(
            [FromBody] SetupPasswordRequest? request,
            CancellationToken cancellationToken
        )
        {
            if (request is null)
            {
                return Errors
                    .ValidationFailed(
                        "The request body is required.",
                        new Dictionary<string, string[]>
                        {
                            ["body"] = ["The request body is required."],
                        }
                    )
                    .ToProblem();
            }

            var invalid = ModelProblem();
            if (invalid is not null)
            {
                return invalid;
            }

            var setup = await _setup.HandleAsync(
                request!.Email,
                request.CurrentSecret,
                request.NewPassword,
                cancellationToken
            );
            if (setup.IsFailure)
            {
                return setup.Error.ToProblem();
            }

            var login = await _login.HandleAsync(
                request.Email,
                request.NewPassword,
                cancellationToken
            );
            return login.IsSuccess ? Results.Ok(login.Value) : login.Error.ToProblem();
        }

        /// <summary>
        /// Changes the password for the signed-in account.
        /// </summary>
        /// <param name="request">The current and new passwords.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 on success; 401 <c>INVALID_CREDENTIALS</c> for a wrong
        /// current secret; 400 with field errors for a weak new password.
        /// </returns>
        [HttpPost("change-password")]
        public async Task<IResult> ChangePasswordAsync(
            [FromBody] ChangePasswordRequest? request,
            CancellationToken cancellationToken
        )
        {
            if (request is null)
            {
                return Errors
                    .ValidationFailed(
                        "The request body is required.",
                        new Dictionary<string, string[]>
                        {
                            ["body"] = ["The request body is required."],
                        }
                    )
                    .ToProblem();
            }

            var invalid = ModelProblem();
            if (invalid is not null)
            {
                return invalid;
            }

            var email = User.FindFirst(IdentityClaimTypes.Email)?.Value;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Errors.Unauthenticated().ToProblem();
            }

            var result = await _change.HandleAsync(
                email,
                request!.CurrentPassword,
                request.NewPassword,
                cancellationToken
            );
            return result.IsSuccess ? Results.Ok() : result.Error.ToProblem();
        }

        /// <summary>
        /// Returns the signed-in caller's email, roles, and permissions from
        /// the bearer token.
        /// </summary>
        /// <returns>200 with the caller snapshot.</returns>
        [HttpGet("me")]
        public ActionResult<MeResponse> Me()
        {
            var email = User.FindFirst(IdentityClaimTypes.Email)?.Value ?? string.Empty;
            var roles = User.FindAll(IdentityClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var permissions = User.FindAll(IdentityClaimTypes.Permission)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return Ok(new MeResponse(email, roles, permissions));
        }

        /// <summary>
        /// Maps data-annotation failures to a coded validation problem.
        /// </summary>
        /// <returns>The problem, or null when the model is valid.</returns>
        private IResult? ModelProblem()
        {
            if (ModelState.IsValid)
            {
                return null;
            }

            var errors = ModelState
                .Where(pair => pair.Value?.Errors.Count > 0)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value!.Errors.Select(error => error.ErrorMessage).ToArray(),
                    StringComparer.Ordinal
                );
            return Errors
                .ValidationFailed("The request contains invalid fields.", errors)
                .ToProblem();
        }
    }

    /// <summary>
    /// Sign-in credentials.
    /// </summary>
    public sealed record LoginRequest
    {
        /// <summary>Gets the account email.</summary>
        [Required, EmailAddress]
        public string? Email { get; init; }

        /// <summary>Gets the clear-text password.</summary>
        [Required]
        public string? Password { get; init; }
    }

    /// <summary>
    /// First-login setup: the seeded secret plus its replacement.
    /// </summary>
    public sealed record SetupPasswordRequest
    {
        /// <summary>Gets the account email.</summary>
        [Required, EmailAddress]
        public string? Email { get; init; }

        /// <summary>Gets the seeded or temporary password.</summary>
        [Required]
        public string? CurrentSecret { get; init; }

        /// <summary>Gets the operator-chosen password.</summary>
        [Required]
        public string? NewPassword { get; init; }
    }

    /// <summary>
    /// Password change for the signed-in account.
    /// </summary>
    public sealed record ChangePasswordRequest
    {
        /// <summary>Gets the current clear-text secret.</summary>
        [Required]
        public string? CurrentPassword { get; init; }

        /// <summary>Gets the operator-chosen password.</summary>
        [Required]
        public string? NewPassword { get; init; }
    }

    /// <summary>
    /// The signed-in caller as the token describes them.
    /// </summary>
    /// <param name="Email">The token email claim.</param>
    /// <param name="Roles">The token role claims.</param>
    /// <param name="Permissions">The token permission claims.</param>
    public sealed record MeResponse(
        string Email,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions
    );
}
