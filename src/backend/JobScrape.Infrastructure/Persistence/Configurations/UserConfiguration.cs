using JobScrape.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobScrape.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// Maps <see cref="User"/>; emails are unique. The password hash is one
    /// <c>iterations:salt:hash</c> column; roles are a JSON array of role
    /// ids with no foreign key, so user writes never block on role rows.
    /// </summary>
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        /// <summary>
        /// Configures the table, conversions, and indexes.
        /// </summary>
        /// <param name="builder">The entity builder.</param>
        public void Configure(EntityTypeBuilder<User> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.ToTable("users");
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id).HasConversion(DomainConverters.UserId);
            builder.Property(user => user.Email).IsRequired().UseCollation("NOCASE");
            builder
                .Property(user => user.PasswordHash)
                .HasConversion(DomainConverters.PasswordHash)
                .HasColumnName("password_hash");
            builder.Property(user => user.MustChangePassword).HasColumnName("must_change_password");
            builder.Property(user => user.TokenVersion).HasColumnName("token_version");
            builder.Property(user => user.FailedAttempts).HasColumnName("failed_attempts");
            builder.Property(user => user.LockedUntilUtc).HasColumnName("locked_until_utc");
            builder.Property(user => user.CreatedAt).HasColumnName("created_at");
            builder
                .Property<List<RoleId>>("_roles")
                .HasConversion(DomainConverters.RoleIdList)
                .HasColumnName("role_ids")
                .Metadata.SetValueComparer(DomainConverters.RoleIdListComparer);
            builder.Ignore(user => user.Roles);
            builder.Ignore(user => user.DomainEvents);
            builder.HasIndex(user => user.Email).IsUnique();
        }
    }
}
