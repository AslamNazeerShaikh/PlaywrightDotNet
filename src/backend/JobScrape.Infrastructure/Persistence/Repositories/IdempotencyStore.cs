using JobScrape.Application.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobScrape.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// EF Core <see cref="IIdempotencyStore"/>. Expired rows (older than
    /// 24 hours) read as absent and are pruned on writes, so the table
    /// cannot grow without bound.
    /// </summary>
    internal sealed partial class IdempotencyStore(
    JobScrapeDbContext db,
    TimeProvider clock,
    ILogger<IdempotencyStore>? logger = null
) : IIdempotencyStore
{
    private readonly ILogger<IdempotencyStore> _logger =
        logger ?? NullLogger<IdempotencyStore>.Instance;
        /// <summary>
        /// How long a stored response replays, in hours.
        /// </summary>
        internal const int RetentionHours = 24;

        /// <summary>
        /// SQLite extended result code for a composite primary-key violation.
        /// </summary>
        private const int SqlitePrimaryKeyConstraintExtendedCode = 1555;

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
            // A reservation may be tracked in this unit of work; drop it so
            // the read reflects committed state from other scopes.
            db.ChangeTracker.Clear();
            var record = await db.IdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    stored =>
                        stored.OwnerKey == ownerKey
                        && stored.Endpoint == endpoint
                        && stored.Key == key,
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
            // Cleanup must complete even when the requesting client has
            // disconnected, so cancellation is never observed here.
            var token = CancellationToken.None;
            try
            {
                await RemoveCoreAsync(ownerKey, endpoint, key, token);
            }
            catch (Exception ex) when (IsBusy(ex))
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(50), token);
                    await RemoveCoreAsync(ownerKey, endpoint, key, token);
                }
                catch (Exception retryEx)
                {
                    LogRemoveFailed(_logger, retryEx, ownerKey, endpoint, key);
                }
            }
            catch (Exception ex)
            {
                LogRemoveFailed(_logger, ex, ownerKey, endpoint, key);
            }
        }

        private async Task RemoveCoreAsync(
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
            await db.SaveChangesAsync(cancellationToken);
        }

        private static bool IsBusy(Exception exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqliteException sqlite)
                {
                    var primary = (int)sqlite.SqliteExtendedErrorCode & 0xFF;
                    if (primary is 5 or 6) // SQLITE_BUSY, SQLITE_LOCKED
                    {
                        return true;
                    }
                }
            }

            return false;
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
                && (
                    sqlite.SqliteExtendedErrorCode
                        == SqliteUniqueConstraintExtendedCode
                    || sqlite.SqliteExtendedErrorCode
                        == SqlitePrimaryKeyConstraintExtendedCode
                );
        }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Idempotency cleanup failed for {OwnerKey}/{Endpoint}/{Key}"
    )]
    private static partial void LogRemoveFailed(
        ILogger logger,
        Exception exception,
        string ownerKey,
        string endpoint,
        string key
    );
}
}
