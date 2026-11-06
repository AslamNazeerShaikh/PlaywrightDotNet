using JobScrape.Application.Abstractions;
using Microsoft.Data.Sqlite;
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

        /// <summary>
        /// SQLite extended result code for a unique constraint violation.
        /// </summary>
        private const int SqliteUniqueConstraintExtendedCode = 2067;

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
            var expired =
                record is not null
                && clock.GetUtcNow() - record.CreatedAt > TimeSpan.FromHours(RetentionHours);
            var completed = record is not null && record.StatusCode != 0 && !expired;
            if (completed)
            {
                // A concurrent writer already finished this triple; keep its
                // stored response.
                return;
            }

            if (record is null || expired)
            {
                if (record is not null && expired)
                {
                    db.IdempotencyRecords.Remove(record);
                    await db.SaveChangesAsync(cancellationToken);
                    record = null;
                }

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
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Lost the insert race against another writer. The first writer
                // owns the stored response; discard this one.
                db.ChangeTracker.Clear();
            }
        }

        /// <inheritdoc />
        public async Task<bool> TryReserveAsync(
            IdempotencyEntry entry,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(entry);
            var record = await db.IdempotencyRecords.FindAsync(
                [entry.OwnerKey, entry.Endpoint, entry.Key],
                cancellationToken
            );

            if (record is not null)
            {
                if (clock.GetUtcNow() - record.CreatedAt <= TimeSpan.FromHours(RetentionHours))
                {
                    // Already reserved or completed by another caller.
                    return false;
                }

                db.IdempotencyRecords.Remove(record);
                await db.SaveChangesAsync(cancellationToken);
            }

            var pending = new IdempotencyRecord
            {
                OwnerKey = entry.OwnerKey,
                Endpoint = entry.Endpoint,
                Key = entry.Key,
                RequestHash = entry.RequestHash,
                StatusCode = 0,
                ContentType = entry.ContentType,
                Body = entry.Body,
                CreatedAt = entry.CreatedAt,
            };
            await db.IdempotencyRecords.AddAsync(pending, cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                db.ChangeTracker.Clear();
                return false;
            }
        }

        /// <inheritdoc />
        public async Task RemoveAsync(
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
                return;
            }

            db.IdempotencyRecords.Remove(record);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Already removed by another caller; nothing to do.
                db.ChangeTracker.Clear();
            }
        }

        /// <summary>
        /// Returns true when the exception is a SQLite unique-constraint
        /// violation, so only genuine lost races are swallowed.
        /// </summary>
        /// <param name="exception">The caught update exception.</param>
        /// <returns>True for a unique violation.</returns>
        private static bool IsUniqueViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqliteException sqlite
                && sqlite.SqliteExtendedErrorCode == SqliteUniqueConstraintExtendedCode;
        }
    }
}
