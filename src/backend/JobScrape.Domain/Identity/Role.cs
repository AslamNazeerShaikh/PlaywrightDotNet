using JobScrape.Domain.Abstractions;

namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// Aggregate root for one named role: a closed <see cref="RoleName"/>
    /// plus its granted <see cref="PermissionKey"/> set, in grant order.
    /// Whether a user keeps at least one admin role is an application-layer
    /// check; this aggregate only guards its own grant set.
    /// </summary>
    public sealed class Role : AggregateRoot
    {
        private readonly List<PermissionKey> _permissions;

        /// <summary>
        /// Initializes a new instance. Private so roles are created through
        /// <see cref="Create"/>.
        /// </summary>
        /// <param name="id">The role identifier.</param>
        /// <param name="name">The closed role name.</param>
        private Role(RoleId id, RoleName name)
        {
            Id = id;
            Name = name;
            _permissions = [];
        }

        /// <summary>
        /// Gets the role identifier.
        /// </summary>
        public RoleId Id { get; }

        /// <summary>
        /// Gets the closed role name.
        /// </summary>
        public RoleName Name { get; }

        /// <summary>
        /// Gets the granted permissions, in grant order.
        /// </summary>
        public IReadOnlyList<PermissionKey> PermissionKeys => _permissions.AsReadOnly();

        /// <summary>
        /// Creates a role with no granted permissions.
        /// </summary>
        /// <param name="id">The role identifier.</param>
        /// <param name="name">The closed role name.</param>
        /// <returns>The role.</returns>
        public static Role Create(RoleId id, RoleName name)
        {
            return new Role(id, name);
        }

        /// <summary>
        /// Grants a permission. Duplicate grants are rejected: the grant
        /// list is a set in grant order.
        /// </summary>
        /// <param name="key">The permission to grant.</param>
        /// <exception cref="DomainException">Thrown when the permission is already granted.</exception>
        public void Grant(PermissionKey key)
        {
            if (_permissions.Contains(key))
            {
                throw new DomainException($"Permission '{key}' is already granted.");
            }

            _permissions.Add(key);
        }

        /// <summary>
        /// Revokes a permission. Revoking a permission that was never
        /// granted is a no-op, so retries converge.
        /// </summary>
        /// <param name="key">The permission to revoke.</param>
        public void Revoke(PermissionKey key)
        {
            _permissions.Remove(key);
        }
    }
}
