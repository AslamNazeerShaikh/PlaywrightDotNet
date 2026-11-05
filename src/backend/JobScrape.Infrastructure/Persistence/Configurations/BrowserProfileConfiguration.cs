using JobScrape.Domain.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="BrowserProfile"/>; names are unique. The user-data
    /// directory is derived, not stored.
    /// </summary>
    public sealed class BrowserProfileConfiguration : IEntityTypeConfiguration<BrowserProfile>
    {
        /// <summary>
        /// Configures the table, conversions, and indexes.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<BrowserProfile> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("browser_profiles");
            builder.HasKey(profile => profile.Id);
            builder
                .Property(profile => profile.Id)
                .HasConversion(DomainConverters.BrowserProfileId);
            builder.Property(profile => profile.Name).IsRequired().UseCollation("NOCASE");
            builder
                .Property(profile => profile.Source)
                .HasConversion(DomainConverters.SourceKey)
                .HasColumnName("source_key");
            builder.Property(profile => profile.Engine).HasConversion<string>();
            builder.Property(profile => profile.Channel).HasConversion<string>();
            builder.Property(profile => profile.State).HasConversion<string>();
            builder.Property(profile => profile.CooldownUntil);
            builder.Property(profile => profile.ConsecutiveChallenges);
            builder.Property(profile => profile.LastUsedAt);
            builder.Property(profile => profile.LastVerifiedAt);
            builder.Ignore(profile => profile.UserDataDirectory);
            builder.Ignore(profile => profile.DomainEvents);
            builder.HasIndex(profile => profile.Name).IsUnique();
        }
    }
}
