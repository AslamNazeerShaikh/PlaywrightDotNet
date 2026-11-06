namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// One catalog entry: a v1 permission key plus its operator-facing
    /// description. Immutable: both properties are init-only.
    /// </summary>
    public sealed record PermissionDefinition
    {
        /// <summary>
        /// Gets the permission key.
        /// </summary>
        public required PermissionKey Key { get; init; }

        /// <summary>
        /// Gets what the permission allows, in operator terms.
        /// </summary>
        public required string Description { get; init; }
    }

    /// <summary>
    /// The stable v1 permission set. The key strings are a compatibility
    /// surface: renaming or removing one breaks stored role grants, so the
    /// catalog test pins the full list.
    /// </summary>
    public static class PermissionCatalog
    {
        /// <summary>
        /// Gets every v1 permission, in a fixed order.
        /// </summary>
        public static IReadOnlyList<PermissionDefinition> All { get; } =
        [
            new PermissionDefinition
            {
                Key = PermissionKey.From("profiles.read"),
                Description = "View search profiles.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("profiles.write"),
                Description = "Create and edit search profiles.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("runs.read"),
                Description = "View scrape runs and their results.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("runs.write"),
                Description = "Start, resume, and cancel scrape runs.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("users.read"),
                Description = "View users and their lockout state.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("users.write"),
                Description = "Create users and reset their lockouts.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("roles.read"),
                Description = "View roles and their permission grants.",
            },
            new PermissionDefinition
            {
                Key = PermissionKey.From("roles.write"),
                Description = "Create roles and change their permission grants.",
            },
        ];

        /// <summary>
        /// Gets every v1 key without its description.
        /// </summary>
        public static IReadOnlyList<PermissionKey> Keys { get; } =
            All.Select(definition => definition.Key).ToList().AsReadOnly();

        /// <summary>
        /// Reports whether <paramref name="key"/> is a known v1 permission.
        /// </summary>
        /// <param name="key">The key to look up.</param>
        /// <returns>True when the catalog contains the key.</returns>
        public static bool Contains(PermissionKey key)
        {
            return Keys.Contains(key);
        }

        /// <summary>
        /// Gets the operator-facing description of a known permission.
        /// </summary>
        /// <param name="key">The key to describe.</param>
        /// <returns>The description.</returns>
        /// <exception cref="DomainException">Thrown when the key is not in the catalog.</exception>
        public static string Describe(PermissionKey key)
        {
            var definition = All.FirstOrDefault(candidate => candidate.Key == key);
            if (definition is null)
            {
                throw new DomainException($"Unknown permission '{key}'.");
            }

            return definition.Description;
        }
    }
}
