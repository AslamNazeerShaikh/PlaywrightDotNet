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
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();
            builder.Services.AddProblemDetails();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddTransient<GetHealthHandler>();

            var app = builder.Build();

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
