namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Identity of the caller invoking a use case. Hosts (API, MCP) resolve
    /// it from scoped dependency injection per request; handlers accept it as
    /// a parameter instead of raw actor strings threaded through calls.
    /// </summary>
    public interface ICallerContext
    {
        /// <summary>
        /// Gets the caller's actor identifier.
        /// </summary>
        string ActorId { get; }

        /// <summary>
        /// Gets the roles granted to the caller.
        /// </summary>
        IReadOnlyCollection<string> Roles { get; }

        /// <summary>
        /// Gets the permission keys granted to the caller.
        /// </summary>
        IReadOnlyCollection<string> Permissions { get; }
    }

    /// <summary>
    /// Default <see cref="ICallerContext"/> for hosts to construct at the
    /// request edge from whatever identity the transport supplies.
    /// </summary>
    /// <param name="ActorId">The caller's actor identifier.</param>
    /// <param name="Roles">The roles granted to the caller.</param>
    /// <param name="Permissions">The permission keys granted to the caller.</param>
    public sealed record CallerContext(
        string ActorId,
        IReadOnlyCollection<string> Roles,
        IReadOnlyCollection<string> Permissions
    ) : ICallerContext
    {
        /// <summary>
        /// Gets the local default: loopback callers with no transport
        /// identity run as this actor until JWT wiring lands.
        /// </summary>
        public static CallerContext LocalOperator { get; } =
            new("local-operator", Array.Empty<string>(), Array.Empty<string>());
    }
}
