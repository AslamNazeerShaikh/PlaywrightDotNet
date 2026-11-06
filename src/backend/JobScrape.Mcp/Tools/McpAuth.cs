using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using ModelContextProtocol;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The MCP permission gate. Every tool except <c>get_health</c> requires
    /// one catalog permission from the caller's bearer token; a caller
    /// without it gets an <c>ACCESS_DENIED</c> tool error (HTTP 403 on the
    /// REST edge). The installed SDK's <see cref="McpException"/> carries
    /// only a message, so the stable code travels inside it.
    /// </summary>
    internal static class McpAuth
    {
        /// <summary>
        /// Throws <c>ACCESS_DENIED</c> unless the caller holds the permission.
        /// </summary>
        /// <param name="caller">The calling actor. Must not be null.</param>
        /// <param name="permission">The required catalog key, e.g. <c>runs.write</c>.</param>
        /// <exception cref="McpException">Thrown when the caller lacks the permission.</exception>
        internal static void RequirePermission(ICallerContext caller, string permission)
        {
            ArgumentNullException.ThrowIfNull(caller);
            ArgumentNullException.ThrowIfNull(permission);
            if (
                caller.MustChangePassword
                || !caller.Permissions.Contains(permission, StringComparer.Ordinal)
            )
            {
                throw McpErrors.From(Errors.AccessDenied());
            }
        }
    }
}
