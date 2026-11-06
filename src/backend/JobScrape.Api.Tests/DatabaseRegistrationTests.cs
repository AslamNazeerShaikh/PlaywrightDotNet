using JobScrape.Infrastructure.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace JobScrape.Api.Tests
{
    public sealed class DatabaseRegistrationTests
    {
        [Fact]
        public async Task MigrationsAreNotAppliedOutsideDevelopment()
        {
            var builder = WebApplication.CreateBuilder(
                new WebApplicationOptions { EnvironmentName = "Production" }
            );
            await using var app = builder.Build();

            Assert.False(DatabaseRegistration.MigrateInDevelopment(app));
        }

        [Fact]
        public void RegistrationCreatesTheDatabaseDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), $"jobscrape-reg-{Guid.NewGuid():N}");
            try
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["JobScrape:Storage:DatabasePath"] = "nested/dir/app.db",
                        }
                    )
                    .Build();

                new ServiceCollection().AddJobScrapeDatabase(configuration, root);

                Assert.True(Directory.Exists(Path.Combine(root, "nested", "dir")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Fact]
        public void GuardsRejectNulls()
        {
            var services = new ServiceCollection();
            var configuration = new ConfigurationBuilder().Build();

            Assert.Throws<ArgumentNullException>(() =>
                DatabaseRegistration.AddJobScrapeDatabase(null!, configuration, "/")
            );
            Assert.Throws<ArgumentNullException>(() => services.AddJobScrapeDatabase(null!, "/"));
            Assert.Throws<ArgumentNullException>(() =>
                services.AddJobScrapeDatabase(configuration, null!)
            );
            Assert.Throws<ArgumentNullException>(() =>
                DatabaseRegistration.MigrateInDevelopment(null!)
            );
        }
    }
}
