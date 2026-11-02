using JobScrape.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="SourceCircuitBreaker"/>, one row per source. Recent
    /// wall times are a JSON array column read through the private field.
    /// </summary>
    public sealed class SourceCircuitBreakerConfiguration
        : IEntityTypeConfiguration<SourceCircuitBreaker>
    {
        /// <summary>
        /// Configures the table, conversions, and key.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<SourceCircuitBreaker> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("source_circuit_breakers");
            builder
                .Property(breaker => breaker.Source)
                .HasConversion(DomainConverters.SourceKey)
                .HasColumnName("source_key");
            builder.HasKey(breaker => breaker.Source);
            builder.Property(breaker => breaker.State).HasConversion<string>();
            builder.Property(breaker => breaker.OpenUntil);
            builder.Property(breaker => breaker.ConsecutiveFailures);
            builder.PrimitiveCollection<List<DateTimeOffset>>("_recentFailures");
            builder.Ignore(breaker => breaker.RecentFailures);
            builder.Ignore(breaker => breaker.AllowsRun);
            builder.Ignore(breaker => breaker.DomainEvents);
        }
    }
}
