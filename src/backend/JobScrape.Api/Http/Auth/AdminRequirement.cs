using Microsoft.AspNetCore.Authorization;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// Requires the admin role on the bearer token. First-login tokens
    /// never satisfy it outside the auth surface.
    /// </summary>
    public sealed record AdminRequirement : IAuthorizationRequirement;
}
