using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IRoleRepository"/>.
    /// </summary>
    internal sealed class RoleRepository(JobScrapeDbContext db) : IRoleRepository
    {
        /// <inheritdoc />
        public async Task<Role?> GetAsync(RoleId id, CancellationToken cancellationToken)
        {
            return await db.Roles.FindAsync([id], cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Role?> GetByNameAsync(RoleName name, CancellationToken cancellationToken)
        {
            return db.Roles.Local.FirstOrDefault(role => role.Name == name)
                ?? await db.Roles.FirstOrDefaultAsync(role => role.Name == name, cancellationToken);
        }

        /// <inheritdoc />
        public async Task AddAsync(Role role, CancellationToken cancellationToken)
        {
            await db.Roles.AddAsync(role, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken)
        {
            return await db.Roles.ToListAsync(cancellationToken);
        }
    }
}
