using JobScrape.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Http
{
    /// <summary>
    /// Maps MVC model-state failures to the API's coded validation problem.
    /// The automatic 400 is suppressed in Program, so every controller with a
    /// data-annotated body funnels through here instead of growing its own
    /// copy.
    /// </summary>
    public static class ModelStateProblems
    {
        /// <summary>
        /// Maps data-annotation failures to a coded validation problem.
        /// </summary>
        /// <param name="controller">The controller holding the model state. Must not be null.</param>
        /// <returns>The problem, or null when the model is valid.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="controller"/> is null.</exception>
        public static IResult? ModelProblem(this ControllerBase controller)
        {
            ArgumentNullException.ThrowIfNull(controller);
            if (controller.ModelState.IsValid)
            {
                return null;
            }

            var errors = controller
                .ModelState.Where(pair => pair.Value?.Errors.Count > 0)
                .ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value!.Errors.Select(error => error.ErrorMessage).ToArray(),
                    StringComparer.Ordinal
                );
            return Errors
                .ValidationFailed("The request contains invalid fields.", errors)
                .ToProblem();
        }
    }
}
