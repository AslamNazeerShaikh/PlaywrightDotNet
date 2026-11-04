using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="ScrapeRunProgress"/>, keyed by its run. One row per
    /// run at most, replaced after every fetched page.
    /// </summary>
    public sealed class ScrapeRunProgressConfiguration : IEntityTypeConfiguration<ScrapeRunProgress>
    {
        /// <summary>
        /// Configures the table, conversions, and key.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<ScrapeRunProgress> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("scrape_run_progress");
            builder.HasKey(progress => progress.Id);
            builder.Property(progress => progress.Id).HasConversion(DomainConverters.ScrapeRunId);
            builder.Property(progress => progress.PagesCompleted);
            builder.Property(progress => progress.NextCursor);
            builder.Property(progress => progress.SeenExternalIds).HasConversion(DomainConverters.StringList);
            builder.Property(progress => progress.UpdatedAt);
        }
    }
}
