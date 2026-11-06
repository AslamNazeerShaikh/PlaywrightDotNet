using System.ComponentModel.DataAnnotations;
using JobScrape.Api.Http;
using JobScrape.Api.Http.Auth;
using JobScrape.Application.Features.Identity;
using JobScrape.Application.Results;
using JobScrape.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// User administration: list, read, create, and role assignment. Admin
    /// role only. Password hashes never leave the store.. Data-annotation failures map to coded validation
    /// problems below because the automatic 400 is suppressed in Program.
    /// </summary>
    [ApiController]
    [Route("api/v1/users")]
    [Authorize(Policy = AuthPolicies.RequireAdmin)]
    public sealed class UsersController : ControllerBase
    {
        // Constructor-injected handlers, never mutated.
        private readonly ListUsersHandler _list;
        private readonly GetUserHandler _get;
        private readonly CreateUserHandler _create;
        private readonly AssignRoleHandler _assign;
        private readonly RevokeRoleHandler _revoke;

        /// <summary>
        /// Initializes a new instance of the <see cref="UsersController"/> class.
        /// </summary>
        /// <param name="list">Lists users.</param>
        /// <param name="get">Gets one user.</param>
        /// <param name="create">Creates users.</param>
        /// <param name="assign">Assigns roles.</param>
        /// <param name="revoke">Revokes roles.</param>
        /// <exception cref="ArgumentNullException">Thrown when any handler is null.</exception>
        public UsersController(
            ListUsersHandler list,
            GetUserHandler get,
            CreateUserHandler create,
            AssignRoleHandler assign,
            RevokeRoleHandler revoke
        )
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(create);
            ArgumentNullException.ThrowIfNull(assign);
            ArgumentNullException.ThrowIfNull(revoke);
            _list = list;
            _get = get;
            _create = create;
            _assign = assign;
            _revoke = revoke;
        }

        /// <summary>
        /// Lists every user with resolved role names.
        /// </summary>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the users.</returns>
        [HttpGet]
        public async Task<ActionResult<UserListResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            return Ok(new UserListResponse(await _list.HandleAsync(cancellationToken)));
        }

        /// <summary>
        /// Gets one user with resolved role names.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the user; 404 <c>USER_NOT_FOUND</c>.</returns>
        [HttpGet("{id:guid}")]
        public async Task<IResult> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var result = await _get.HandleAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Creates a user with a temporary password. The account must
        /// complete first login before it can sign in.
        /// </summary>
        /// <param name="request">The email and temporary password.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 201 with the new user id and its <c>Location</c>; 409
        /// <c>EMAIL_TAKEN</c>; 400 with field errors.
        /// </returns>
        [HttpPost]
        public async Task<IResult> CreateAsync(
            [FromBody] CreateUserRequest? request,
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

            var result = await _create.HandleAsync(
                request!.Email,
                request.TemporaryPassword,
                cancellationToken
            );
            return result.IsSuccess
                ? Results.Created($"/api/v1/users/{result.Value}", new { id = result.Value })
                : result.Error.ToProblem();
        }

        /// <summary>
        /// Assigns a role to a user.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <param name="request">The role to assign.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 on success; 404 <c>USER_NOT_FOUND</c> or
        /// <c>ROLE_NOT_FOUND</c>; 409 <c>ROLE_ALREADY_ASSIGNED</c>.
        /// </returns>
        [HttpPost("{id:guid}/roles")]
        public async Task<IResult> AssignRoleAsync(
            Guid id,
            [FromBody] RoleAssignmentRequest? request,
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

            if (!TryParseRole(request!.Role, out var role))
            {
                return Errors
                    .ValidationFailed(
                        $"Role '{request.Role}' is unknown. Use Admin, Operator, or Viewer.",
                        new Dictionary<string, string[]>
                        {
                            ["role"] = [$"Role '{request.Role}' is unknown."],
                        }
                    )
                    .ToProblem();
            }

            var result = await _assign.HandleAsync(id, role, cancellationToken);
            return result.IsSuccess ? Results.Ok() : result.Error.ToProblem();
        }

        /// <summary>
        /// Revokes a role from a user. Revoking the last admin is refused.
        /// </summary>
        /// <param name="id">The user identifier.</param>
        /// <param name="role">The role to revoke.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 on success; 404 <c>USER_NOT_FOUND</c> or
        /// <c>ROLE_NOT_FOUND</c>; 409 <c>LAST_ADMIN_REQUIRED</c>.
        /// </returns>
        [HttpDelete("{id:guid}/roles/{role}")]
        public async Task<IResult> RevokeRoleAsync(
            Guid id,
            string role,
            CancellationToken cancellationToken
        )
        {
            if (!TryParseRole(role, out var name))
            {
                return Errors
                    .ValidationFailed(
                        $"Role '{role}' is unknown. Use Admin, Operator, or Viewer.",
                        new Dictionary<string, string[]>
                        {
                            ["role"] = [$"Role '{role}' is unknown."],
                        }
                    )
                    .ToProblem();
            }

            var result = await _revoke.HandleAsync(id, name, cancellationToken);
            return result.IsSuccess ? Results.Ok() : result.Error.ToProblem();
        }

        /// <summary>
        /// Parses a closed role name, case-insensitively.
        /// </summary>
        /// <param name="raw">The raw role name.</param>
        /// <param name="name">The parsed name.</param>
        /// <returns>True when the name is a defined role.</returns>
        private static bool TryParseRole(string? raw, out RoleName name)
        {
            return Enum.TryParse(raw, ignoreCase: true, out name) && Enum.IsDefined(name);
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
    /// The payload for creating a user.
    /// </summary>
    public sealed record CreateUserRequest
    {
        /// <summary>Gets the account email.</summary>
        [Required, EmailAddress]
        public string? Email { get; init; }

        /// <summary>Gets the temporary password.</summary>
        [Required]
        public string? TemporaryPassword { get; init; }
    }

    /// <summary>
    /// The payload for assigning a role.
    /// </summary>
    public sealed record RoleAssignmentRequest
    {
        /// <summary>Gets the role name: Admin, Operator, or Viewer.</summary>
        [Required]
        public string? Role { get; init; }
    }
}
