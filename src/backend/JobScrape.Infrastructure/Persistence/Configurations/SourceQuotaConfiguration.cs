using JobScrape.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="SourceQuota"/>, keyed by <c>(source_key, quota_date)</c>
    /// so a day can only be started once.
    /// </summary>
    public sealed class SourceQuotaConfiguration : IEntityTypeConfiguration<SourceQuota>
    {
        /// <summary>
        /// Configures the table, conversions, and key.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<SourceQuota> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("source_quotas");
            builder
                .Property(quota => quota.Source)
                .HasConversion(DomainConverters.SourceKey)
                .HasColumnName("source_key");
            builder.Property(quota => quota.QuotaDate);
            builder.HasKey(quota => new { quota.Source, quota.QuotaDate });
            builder.Property(quota => quota.PagesFetched);
            builder.Property(quota => quota.DetailsFetched);
            builder.Property(quota => quota.RunsStarted);
            builder.Property(quota => quota.Challenges);
            builder.Property(quota => quota.UpdatedAt);
            builder.Ignore(quota => quota.DomainEvents);
        }
    }
}
