using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="ScrapeRun"/>, including the private resume stage so a
    /// paused run resumes where it stopped after a restart. The profile is
    /// referenced by id with a real foreign key but no navigation property.
    /// </summary>
    public sealed class ScrapeRunConfiguration : IEntityTypeConfiguration<ScrapeRun>
    {
        /// <summary>
        /// Configures the table, conversions, keys, and indexes.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<ScrapeRun> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("scrape_runs");
            builder.HasKey(run => run.Id);
            builder.Property(run => run.Id).HasConversion(DomainConverters.ScrapeRunId);
            builder.Property(run => run.ProfileId).HasConversion(DomainConverters.SearchProfileId);
            builder
                .Property(run => run.Source)
                .HasConversion(DomainConverters.SourceKey)
                .HasColumnName("source_key");
            builder.Property(run => run.Limits).HasConversion(DomainConverters.RunLimits);
            builder.Property(run => run.Stage).HasConversion<string>();
            builder.Property(run => run.Stats).HasConversion(DomainConverters.RunStats);
            builder.Property(run => run.CreatedAt);
            builder.Property(run => run.StartedAt);
            builder.Property(run => run.FinishedAt);
            builder.Property(run => run.PauseReason).HasConversion<string>();
            builder.Property(run => run.RetryAt);
            builder.Property(run => run.FailureReason);
            builder.Property<RunStage?>("_resumeStage").HasConversion<string>();
            builder.Ignore(run => run.IsTerminal);
            builder.Ignore(run => run.DomainEvents);
            builder
                .HasOne<SearchProfile>()
                .WithMany()
                .HasForeignKey(run => run.ProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(run => run.CreatedAt);
        }
    }
}
