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
    }
}
