using JobScrape.Domain.Sourcing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="SearchProfile"/>; names are unique.
    /// </summary>
    public sealed class SearchProfileConfiguration : IEntityTypeConfiguration<SearchProfile>
    {
        /// <summary>
        /// Configures the table, conversions, and indexes.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<SearchProfile> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("search_profiles");
            builder.HasKey(profile => profile.Id);
            builder.Property(profile => profile.Id).HasConversion(DomainConverters.SearchProfileId);
            builder.Property(profile => profile.Name).IsRequired().UseCollation("NOCASE");
            builder
                .Property(profile => profile.Source)
                .HasConversion(DomainConverters.SourceKey)
                .HasColumnName("source_key");
            builder
                .Property(profile => profile.Criteria)
                .HasConversion(DomainConverters.SearchCriteria);
            builder.Property(profile => profile.Limits).HasConversion(DomainConverters.RunLimits);
            builder.Property(profile => profile.Enabled);
            builder.Property(profile => profile.CreatedAt);
            builder.Property(profile => profile.UpdatedAt);
            builder.Ignore(profile => profile.DomainEvents);
            builder.HasIndex(profile => profile.Name).IsUnique();
        }
    }
}
