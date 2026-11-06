using JobScrape.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IIdempotencyStore"/>. Expired rows (older than
    /// 24 hours) read as absent and are pruned on writes, so the table
    /// cannot grow without bound.
    /// </summary>
    internal sealed class IdempotencyStore(JobScrapeDbContext db, TimeProvider clock)
        : IIdempotencyStore
    {
        /// <summary>
        /// How long a stored response replays, in hours.
        /// </summary>
        internal const int RetentionHours = 24;

        /// <inheritdoc />
        public async Task<IdempotencyEntry?> GetAsync(
            string ownerKey,
            string endpoint,
            string key,
            CancellationToken cancellationToken
        )
        {
            var record = await db.IdempotencyRecords.FindAsync(
                [ownerKey, endpoint, key],
                cancellationToken
            );
            if (record is null)
            {
                return null;
            }

            if (clock.GetUtcNow() - record.CreatedAt > TimeSpan.FromHours(RetentionHours))
            {
                return null;
            }

            return new IdempotencyEntry(
                record.OwnerKey,
                record.Endpoint,
                record.Key,
                record.RequestHash,
                record.StatusCode,
                record.ContentType,
                record.Body,
                record.CreatedAt
            );
        }

        /// <inheritdoc />
        public async Task SaveAsync(IdempotencyEntry entry, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(entry);
            var record = await db.IdempotencyRecords.FindAsync(
                [entry.OwnerKey, entry.Endpoint, entry.Key],
                cancellationToken
            );
            if (record is null)
            {
                record = new IdempotencyRecord
                {
                    OwnerKey = entry.OwnerKey,
                    Endpoint = entry.Endpoint,
                    Key = entry.Key,
                };
                await db.IdempotencyRecords.AddAsync(record, cancellationToken);
            }

            record.StatusCode = entry.StatusCode;
            record.RequestHash = entry.RequestHash;
            record.ContentType = entry.ContentType;
            record.Body = entry.Body;
            record.CreatedAt = entry.CreatedAt;

            var cutoff = clock.GetUtcNow() - TimeSpan.FromHours(RetentionHours);
            await db
                .IdempotencyRecords.Where(stored => stored.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
