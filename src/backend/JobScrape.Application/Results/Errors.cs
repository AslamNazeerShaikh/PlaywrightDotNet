namespace JobScrape.Application.Results
{
    /// <summary>
    /// Stable <see cref="ErrorInfo"/> factories. Codes are the UI contract:
    /// uppercase snake case, never localized, never repurposed once shipped.
    /// </summary>
    public static class Errors
    {
        /// <summary>
        /// Creates a validation failure carrying field-level errors.
        /// </summary>
        /// <param name="detail">Operator-safe detail describing the request problem.</param>
        /// <param name="errors">Field errors keyed by JSON request field name.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.Validation"/>.</returns>
        public static ErrorInfo ValidationFailed(
            string detail,
            IReadOnlyDictionary<string, string[]> errors
        )
        {
            ArgumentNullException.ThrowIfNull(detail);
            ArgumentNullException.ThrowIfNull(errors);
            return new ErrorInfo(
                "VALIDATION_FAILED",
                "One or more validation errors occurred.",
                detail,
                ErrorKind.Validation,
                new Dictionary<string, object?> { ["errors"] = errors }
            );
        }

        /// <summary>
        /// Creates a not-found failure for a missing search profile.
        /// </summary>
        /// <param name="profileId">The requested profile identifier, echoed in metadata.</param>
        /// <returns>An <see cref="ErrorInfo"/> with kind <see cref="ErrorKind.NotFound"/>.</returns>
        public static ErrorInfo ProfileNotFound(Guid profileId)
        {
            return new ErrorInfo(
                "PROFILE_NOT_FOUND",
                "Search profile not found.",
                $"Search profile '{profileId}' does not exist.",
                ErrorKind.NotFound,
                new Dictionary<string, object?> { ["profileId"] = profileId }
            );
        }
    }
}
