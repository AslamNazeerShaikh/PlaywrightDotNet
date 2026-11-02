using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="JobPosting"/>. The unique
    /// <c>(source_key, external_job_id)</c> index is the dedupe guarantee;
    /// skills are a JSON array of display names.
    /// </summary>
    public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
    {
        /// <summary>
        /// Configures the table, conversions, and indexes.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<JobPosting> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("job_postings");
            builder.HasKey(posting => posting.Id);
            builder.Property(posting => posting.Id).HasConversion(DomainConverters.JobPostingId);
            builder
                .Property(posting => posting.Source)
                .HasConversion(DomainConverters.SourceKey)
                .HasColumnName("source_key");
            builder
                .Property(posting => posting.ExternalJobId)
                .HasConversion(DomainConverters.ExternalJobId);
            builder
                .Property(posting => posting.Url)
                .HasConversion(DomainConverters.CanonicalUrl)
                .HasColumnName("canonical_url");
            builder.Property(posting => posting.Title).HasConversion(DomainConverters.JobTitle);
            builder
                .Property(posting => posting.Company)
                .HasConversion(DomainConverters.CompanyName);
            builder.Property(posting => posting.CompanyUrl);
            builder
                .Property(posting => posting.Location)
                .HasConversion(DomainConverters.JobLocation);
            builder.Property(posting => posting.ExperienceRaw);
            builder.Property(posting => posting.ExperienceMinYears);
            builder.Property(posting => posting.ExperienceMaxYears);
            builder.Property(posting => posting.SalaryRaw);
            builder.Property(posting => posting.Salary).HasConversion(DomainConverters.SalaryRange);
            builder.Property(posting => posting.PostedRaw);
            builder.Property(posting => posting.PostedAt);
            builder.Property(posting => posting.Description);
            builder.Property(posting => posting.Snippet);
            builder
                .Property(posting => posting.Skills)
                .HasConversion(DomainConverters.Skills, DomainConverters.SkillsComparer);
            builder.Property(posting => posting.FirstSeenAt);
            builder.Property(posting => posting.LastSeenAt);
            builder
                .Property(posting => posting.ContentHash)
                .HasConversion(DomainConverters.ContentHash);
            builder.Property(posting => posting.Status).HasConversion<string>();
            builder.Property(posting => posting.QualityIssues);
            builder.Property<string?>("_applyUrl");
            builder.Ignore(posting => posting.ApplyUrl);
            builder.Ignore(posting => posting.DomainEvents);
            builder.HasIndex(posting => new { posting.Source, posting.ExternalJobId }).IsUnique();
            builder.HasIndex(posting => posting.LastSeenAt);
            builder.HasIndex(posting => posting.Company);
            builder.HasIndex(posting => posting.PostedAt);
        }
    }
}
