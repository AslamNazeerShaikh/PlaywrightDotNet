using JobScrape.Application.Features.Health;

namespace JobScrape.Api
{
    /// <summary>
    /// Composition root. Wiring only; verified by the API smoke and
    /// integration tests rather than unit coverage.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class Program
    {
        /// <summary>
        /// Application entry point. Builds the host, wires services, and
        /// blocks serving requests until shutdown.
        /// </summary>
        /// <param name="args">Command-line arguments forwarded to the host builder.</param>
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Controllers first: the API surface is controller-based so every
            // endpoint stays greppable and block-namespaced (no minimal-API
            // lambdas hidden in this method).
            builder.Services.AddControllers();

            // OpenAPI feeds Scalar locally and the cockpit codegen; served only
            // in Development (see the MapOpenApi guard below).
            builder.Services.AddOpenApi();

            // RFC 9457 problem details for the global exception handler.
            builder.Services.AddProblemDetails();

            // System clock as the ambient time source; handlers take
            // TimeProvider so tests can substitute a fake.
            builder.Services.AddSingleton(TimeProvider.System);

            // Explicit use-case handlers, registered one by one. No assembly
            // scanning, no mediator: "Find usages" on a handler finds its
            // callers.
            builder.Services.AddTransient<GetHealthHandler>();

            var app = builder.Build();

            // Development-only document endpoint; never proxied by the
            // cockpit BFF and never exposed beyond loopback.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseExceptionHandler();
            app.MapControllers();
            app.Run();
        }
    }
}
