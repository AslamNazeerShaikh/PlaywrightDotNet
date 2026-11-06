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
    /// Role administration: list, read, create, and permission grants. Admin
    /// role or a matching grant. Data-annotation failures map to coded
    /// validation problems below because the automatic 400 is suppressed in
    /// Program.
    /// </summary>
    [ApiController]
    [Route("api/v1/roles")]
    public sealed class RolesController : ControllerBase
    {
        // Constructor-injected handlers, never mutated.
        private readonly ListRolesHandler _list;
        private readonly GetRoleHandler _get;
        private readonly CreateRoleHandler _create;
        private readonly GrantPermissionHandler _grant;
        private readonly RevokePermissionHandler _revoke;

        /// <summary>
        /// Initializes a new instance of the <see cref="RolesController"/> class.
        /// </summary>
        /// <param name="list">Lists roles.</param>
        /// <param name="get">Gets one role.</param>
        /// <param name="create">Creates roles.</param>
        /// <param name="grant">Grants permissions.</param>
        /// <param name="revoke">Revokes permissions.</param>
        /// <exception cref="ArgumentNullException">Thrown when any handler is null.</exception>
        public RolesController(
            ListRolesHandler list,
            GetRoleHandler get,
            CreateRoleHandler create,
            GrantPermissionHandler grant,
            RevokePermissionHandler revoke
        )
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(create);
            ArgumentNullException.ThrowIfNull(grant);
            ArgumentNullException.ThrowIfNull(revoke);
            _list = list;
            _get = get;
            _create = create;
            _grant = grant;
            _revoke = revoke;
        }

        /// <summary>
        /// Lists every role with granted permission keys.
        /// </summary>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the roles.</returns>
        [HttpGet]
        [Authorize(Policy = AuthPolicies.RolesRead)]
        public async Task<ActionResult<RoleListResponse>> ListAsync(
            CancellationToken cancellationToken
        )
        {
            return Ok(new RoleListResponse(await _list.HandleAsync(cancellationToken)));
        }

        /// <summary>
        /// Gets one role by name.
        /// </summary>
        /// <param name="name">The role name: Admin, Operator, or Viewer.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 with the role; 404 <c>ROLE_NOT_FOUND</c>.</returns>
        [HttpGet("{name}")]
        [Authorize(Policy = AuthPolicies.RolesRead)]
        public async Task<IResult> GetAsync(string name, CancellationToken cancellationToken)
        {
            if (!TryParseRole(name, out var role))
            {
                return Errors.RoleNotFound(name).ToProblem();
            }

            var result = await _get.HandleAsync(role, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();
        }

        /// <summary>
        /// Creates a role with no granted permissions.
        /// </summary>
        /// <param name="request">The role to create.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 201 with the new role id and its <c>Location</c>; 409
        /// <c>ROLE_NAME_TAKEN</c> when it exists.
        /// </returns>
        [HttpPost]
        [Authorize(Policy = AuthPolicies.RolesWrite)]
        public async Task<IResult> CreateAsync(
            [FromBody] CreateRoleRequest? request,
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

            if (!TryParseRole(request!.Name, out var role))
            {
                return Errors
                    .ValidationFailed(
                        $"Role '{request.Name}' is unknown. Use Admin, Operator, or Viewer.",
                        new Dictionary<string, string[]>
                        {
                            ["name"] = [$"Role '{request.Name}' is unknown."],
                        }
                    )
                    .ToProblem();
            }

            var result = await _create.HandleAsync(role, cancellationToken);
            return result.IsSuccess
                ? Results.Created($"/api/v1/roles/{role}", new { id = result.Value })
                : result.Error.ToProblem();
        }

        /// <summary>
        /// Grants a permission to a role.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <param name="request">The permission key, e.g. <c>runs.read</c>.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>
        /// 200 on success; 404 <c>ROLE_NOT_FOUND</c>; 400
        /// <c>UNKNOWN_PERMISSION</c> outside the catalog; 409
        /// <c>PERMISSION_ALREADY_GRANTED</c>.
        /// </returns>
        [HttpPost("{name}/permissions")]
        [Authorize(Policy = AuthPolicies.RolesWrite)]
        public async Task<IResult> GrantPermissionAsync(
            string name,
            [FromBody] PermissionGrantRequest? request,
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

            if (!TryParseRole(name, out var role))
            {
                return Errors.RoleNotFound(name).ToProblem();
            }

            if (
                role == RoleName.Admin
                && !User.IsInRole(RoleName.Admin.ToString())
            )
            {
                return Errors.AccessDenied().ToProblem();
            }

            var result = await _grant.HandleAsync(role, request!.Key, cancellationToken);
            return result.IsSuccess ? Results.Ok() : result.Error.ToProblem();
        }

        /// <summary>
        /// Revokes a permission from a role.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <param name="key">The permission key.</param>
        /// <param name="cancellationToken">Aborted when the client disconnects.</param>
        /// <returns>200 on success; 404 <c>ROLE_NOT_FOUND</c>.</returns>
        [HttpDelete("{name}/permissions/{key}")]
        [Authorize(Policy = AuthPolicies.RolesWrite)]
        public async Task<IResult> RevokePermissionAsync(
            string name,
            string key,
            CancellationToken cancellationToken
        )
        {
            if (!TryParseRole(name, out var role))
            {
                return Errors.RoleNotFound(name).ToProblem();
            }

            if (
                role == RoleName.Admin
                && !User.IsInRole(RoleName.Admin.ToString())
            )
            {
                return Errors.AccessDenied().ToProblem();
            }

            var result = await _revoke.HandleAsync(role, key, cancellationToken);
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
    /// The payload for creating a role.
    /// </summary>
    public sealed record CreateRoleRequest
    {
        /// <summary>Gets the role name: Admin, Operator, or Viewer.</summary>
        [Required]
        public string? Name { get; init; }
    }

    /// <summary>
    /// The payload for granting a permission.
    /// </summary>
    public sealed record PermissionGrantRequest
    {
        /// <summary>Gets the permission key, e.g. <c>runs.read</c>.</summary>
        [Required]
        public string? Key { get; init; }
    }
}
