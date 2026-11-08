using Microsoft.AspNetCore.Authorization;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Requires one <c>perm</c> claim on the bearer token.
    /// </summary>
    /// <param name="Permission">The required permission key, e.g. <c>runs.read</c>.</param>
    public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;
}
