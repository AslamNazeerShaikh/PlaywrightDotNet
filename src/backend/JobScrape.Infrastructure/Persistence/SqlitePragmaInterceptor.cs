using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// Applies the SQLite pragmas from docs/02-architecture.md §10.3 on every
    /// opened connection: WAL journaling, enforced foreign keys, and a 5 s
    /// busy timeout so a second connection waits instead of failing.
    /// </summary>
    public sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        /// <summary>
        /// The pragmas applied to each connection.
        /// </summary>
        public const string Pragmas =
            "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";

        /// <summary>
        /// Applies the pragmas after a synchronous open.
        /// </summary>
        /// <param name="connection">The opened connection.</param>
        /// <param name="eventData">Event context.</param>
        public override void ConnectionOpened(
            DbConnection connection,
            ConnectionEndEventData eventData
        )
        {
            ArgumentNullException.ThrowIfNull(connection);
            using var command = connection.CreateCommand();
            command.CommandText = Pragmas;
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Applies the pragmas after an asynchronous open.
        /// </summary>
        /// <param name="connection">The opened connection.</param>
        /// <param name="eventData">Event context.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>A task that completes when the pragmas are applied.</returns>
        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default
        )
        {
            ArgumentNullException.ThrowIfNull(connection);
            await using var command = connection.CreateCommand();
            command.CommandText = Pragmas;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
