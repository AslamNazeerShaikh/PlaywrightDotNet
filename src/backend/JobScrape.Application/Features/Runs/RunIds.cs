using JobScrape.Application.Results;

namespace JobScrape.Application.Features.Runs
{
    /// <summary>
    /// Shared validation for run ids in routes.
    /// </summary>
    internal static class RunIds
    {
        /// <summary>
        /// The validation failure for an empty run id.
        /// </summary>
        /// <returns>A <c>VALIDATION_FAILED</c> error keyed <c>id</c>.</returns>
        public static ErrorInfo Required()
        {
            return Errors.ValidationFailed(
                "The request contains invalid fields.",
                new Dictionary<string, string[]> { ["id"] = ["A run id is required."] }
            );
        }

        /// <summary>
        /// The shared shape for invalid run write inputs: one
        /// <c>VALIDATION_FAILED</c> carrying every offending field.
        /// </summary>
        /// <param name="errors">Field errors keyed by JSON request field name. Must not be null.</param>
        /// <returns>A <c>VALIDATION_FAILED</c> error.</returns>
        public static ErrorInfo InvalidFields(IReadOnlyDictionary<string, string[]> errors)
        {
            ArgumentNullException.ThrowIfNull(errors);
            return Errors.ValidationFailed("The request contains invalid fields.", errors);
        }
    }
}
