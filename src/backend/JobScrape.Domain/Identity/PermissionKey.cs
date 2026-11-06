namespace JobScrape.Domain.Identity
{
    /// <summary>
    /// A granular permission in <c>scope.action</c> form, e.g.
    /// <c>profiles.write</c>. Branded so a permission can never be confused
    /// with an email, a role name, or another plain string.
    /// </summary>
    public readonly record struct PermissionKey
    {
        /// <summary>
        /// Initializes a new instance. Private so keys are created through
        /// the validating <see cref="From"/>.
        /// </summary>
        /// <param name="value">The normalized key.</param>
        private PermissionKey(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the normalized key, lowercase invariant.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Parses a permission key, normalizing case and surrounding
        /// whitespace.
        /// </summary>
        /// <param name="value">The raw key, e.g. <c>profiles.write</c>.</param>
        /// <returns>The typed key.</returns>
        /// <exception cref="DomainException">
        /// Thrown when <paramref name="value"/> is blank, too long, uses
        /// characters outside <c>a-z 0-9 . - _</c>, or has no scope
        /// (no <c>.</c> separator).
        /// </exception>
        public static PermissionKey From(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
            if (
                normalized.Length == 0
                || normalized.Length > 64
                || !normalized.Contains('.', StringComparison.Ordinal)
                || !IsKeyChars(normalized)
            )
            {
                throw new DomainException(
                    "A permission key looks like 'scope.action' using a-z, 0-9, '.', '-', '_'."
                );
            }

            return new PermissionKey(normalized);
        }

        /// <summary>
        /// Returns the normalized key.
        /// </summary>
        /// <returns>The key as a string.</returns>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Reports whether every character is legal in a permission key.
        /// </summary>
        /// <param name="value">The normalized candidate.</param>
        /// <returns>True when only key characters are present.</returns>
        private static bool IsKeyChars(string value)
        {
            foreach (var c in value)
            {
                var legal =
                    (c >= 'a' && c <= 'z')
                    || (c >= '0' && c <= '9')
                    || c is '.' or '-' or '_';
                if (!legal)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
