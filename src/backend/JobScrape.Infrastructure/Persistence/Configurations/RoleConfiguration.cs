using JobScrape.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="Role"/>; the closed name is unique. Permissions are
    /// a JSON array of key strings pinned to the v1 catalog.
    /// </summary>
    public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        /// <summary>
        /// Configures the table, conversions, and indexes.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("roles");
            builder.HasKey(role => role.Id);
            builder.Property(role => role.Id).HasConversion(DomainConverters.RoleId);
            builder.Property(role => role.Name).HasConversion<string>().IsRequired();
            builder
                .Property<List<PermissionKey>>("_permissions")
                .HasConversion(DomainConverters.PermissionList)
                .HasColumnName("permission_keys")
                .Metadata.SetValueComparer(DomainConverters.PermissionListComparer);
            builder.Ignore(role => role.PermissionKeys);
            builder.Ignore(role => role.DomainEvents);
            builder.HasIndex(role => role.Name).IsUnique();
        }
    }
}
