using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="IdempotencyRecord"/> to <c>idempotency_keys</c>. The
    /// triple (owner, endpoint, key) is the primary key, so a repeated store
    /// replaces the previous response for the same triple.
    /// </summary>
    internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
    {
        /// <summary>
        /// Configures the table and composite key.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("idempotency_keys");
            builder.HasKey(record => new
            {
                record.OwnerKey,
                record.Endpoint,
                record.Key,
            });
            builder.Property(record => record.OwnerKey).HasColumnName("owner_key");
            builder.Property(record => record.Endpoint).HasColumnName("endpoint");
            builder.Property(record => record.Key).HasColumnName("key");
            builder.Property(record => record.RequestHash).HasColumnName("request_hash");
            builder.Property(record => record.StatusCode).HasColumnName("status_code");
            builder.Property(record => record.ContentType).HasColumnName("content_type");
            builder.Property(record => record.Body).HasColumnName("body");
            builder.Property(record => record.CreatedAt).HasColumnName("created_at");
        }
    }
}
