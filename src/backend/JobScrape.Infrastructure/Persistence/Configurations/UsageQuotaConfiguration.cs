using JobScrape.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="UsageQuota"/>, keyed by scope so each surface has
    /// exactly one definition. The window travels as ticks; reads go back
    /// through the validating factory.
    /// </summary>
    public sealed class UsageQuotaConfiguration : IEntityTypeConfiguration<UsageQuota>
    {
        /// <summary>
        /// Configures the table, conversions, and key.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<UsageQuota> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("usage_quotas");
            builder.HasKey(quota => quota.Scope);
            builder
                .Property(quota => quota.Scope)
                .HasConversion<string>(
                    scope => scope.ToString(),
                    value => Enum.Parse<QuotaScope>(value, ignoreCase: false)
                );
            builder.Property(quota => quota.Limit);
            builder
                .Property(quota => quota.Window)
                .HasConversion<long>(window => window.Ticks, ticks => TimeSpan.FromTicks(ticks))
                .HasColumnName("window_ticks");
            builder.Ignore(quota => quota.DomainEvents);
        }
    }
}
