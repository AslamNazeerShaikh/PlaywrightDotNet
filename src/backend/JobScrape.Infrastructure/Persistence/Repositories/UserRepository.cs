using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IUserRepository"/>. Email lookup normalizes the
    /// input the same way the domain normalizes stored emails.
    /// </summary>
    internal sealed class UserRepository(JobScrapeDbContext db) : IUserRepository
    {
        /// <inheritdoc />
        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            var normalized = email.Trim().ToLowerInvariant();
            return db.Users.Local.FirstOrDefault(user =>
                    string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase)
                )
                ?? await db.Users.FirstOrDefaultAsync(
                    user => EF.Functions.Collate(user.Email, "NOCASE") == normalized,
                    cancellationToken
                );
        }

        /// <inheritdoc />
        public async Task<User?> GetAsync(UserId id, CancellationToken cancellationToken)
        {
            return await db.Users.FindAsync([id], cancellationToken);
        }

        /// <inheritdoc />
        public async Task AddAsync(User user, CancellationToken cancellationToken)
        {
            await db.Users.AddAsync(user, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken)
        {
            return await db.Users.ToListAsync(cancellationToken);
        }
    }
}
