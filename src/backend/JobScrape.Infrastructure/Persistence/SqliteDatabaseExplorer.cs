using System.Text.Json;
using JobScrape.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// ADO.NET-backed <see cref="IDatabaseExplorer"/> over the SQLite file.
    /// Table/column identifiers are validated against the live schema and
    /// quoted; values are always parameterized.
    /// </summary>
    public sealed class SqliteDatabaseExplorer(string connectionString) : IDatabaseExplorer
    {
        private readonly string _connectionString = connectionString;

        private static bool IsValidIdentifier(string name) =>
            !string.IsNullOrWhiteSpace(name)
            && System.Text.RegularExpressions.Regex.IsMatch(
                name,
                @"^[A-Za-z_][A-Za-z0-9_]*$"
            );

        private static string Quote(string identifier) =>
            $"\"{identifier.Replace("\"", "\"\"")}\"";

        private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
        {
            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync(ct);
                using var pragma = connection.CreateCommand();
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                await pragma.ExecuteNonQueryAsync(ct);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        private static async Task<HashSet<string>> GetUserTablesAsync(
            SqliteConnection connection,
            CancellationToken ct
        )
        {
            var tables = new HashSet<string>(StringComparer.Ordinal);
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static async Task EnsureTableAsync(
            SqliteConnection connection,
            string tableName,
            CancellationToken ct
        )
        {
            if (!IsValidIdentifier(tableName))
            {
                throw new ArgumentException("Invalid table name.", nameof(tableName));
            }

            var tables = await GetUserTablesAsync(connection, ct);
            if (!tables.Contains(tableName))
            {
                throw new TableNotFoundException(tableName);
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DatabaseTableInfo>> ListTablesAsync(
            CancellationToken cancellationToken
        )
        {
            using var connection = await OpenAsync(cancellationToken);
            var tables = await GetUserTablesAsync(connection, cancellationToken);
            var result = new List<DatabaseTableInfo>();
            foreach (var table in tables.OrderBy(t => t, StringComparer.Ordinal))
            {
                if (!IsValidIdentifier(table))
                {
                    continue;
                }

                long count;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = $"SELECT COUNT(*) FROM {Quote(table)};";
                    count = (long)(await cmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
                }

                result.Add(new DatabaseTableInfo(table, count));
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<DatabaseTableSchema?> GetSchemaAsync(
            string tableName,
            CancellationToken cancellationToken
        )
        {
            using var connection = await OpenAsync(cancellationToken);
            await EnsureTableAsync(connection, tableName, cancellationToken);
            return await GetSchemaInnerAsync(connection, tableName, cancellationToken);
        }

        private static async Task<DatabaseTableSchema> GetSchemaInnerAsync(
            SqliteConnection connection,
            string tableName,
            CancellationToken cancellationToken
        )
        {
            var columns = new List<DatabaseColumnInfo>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA table_info({Quote(tableName)});";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    columns.Add(
                        new DatabaseColumnInfo(
                            reader.GetString(1),
                            reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            reader.GetInt32(3) == 1,
                            reader.GetInt32(5) != 0,
                            reader.IsDBNull(4) ? null : reader.GetString(4)
                        )
                    );
                }
            }

            var foreignKeys = new List<DatabaseForeignKeyInfo>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA foreign_key_list({Quote(tableName)});";
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    foreignKeys.Add(
                        new DatabaseForeignKeyInfo(
                            reader.GetString(3),
                            reader.GetString(2),
                            reader.GetString(4)
                        )
                    );
                }
            }

            var pkColumn =
                columns.FirstOrDefault(c => c.PrimaryKey)?.Name
                ?? (columns.Count > 0 ? columns[0].Name : string.Empty);

            return new DatabaseTableSchema(tableName, columns, foreignKeys, pkColumn);
        }

        /// <inheritdoc />
        public async Task<DatabaseRowsResult> ListRowsAsync(
            string tableName,
            int offset,
            int limit,
            CancellationToken cancellationToken
        )
        {
            using var connection = await OpenAsync(cancellationToken);
            await EnsureTableAsync(connection, tableName, cancellationToken);

            limit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 500);
            offset = Math.Max(0, offset);

            long total;
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM {Quote(tableName)};";
                total = (long)(await countCmd.ExecuteScalarAsync(cancellationToken) ?? 0L);
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"SELECT * FROM {Quote(tableName)} LIMIT @limit OFFSET @offset;";
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@offset", offset);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                var columnNames = Enumerable
                    .Range(0, reader.FieldCount)
                    .Select(i => reader.GetName(i))
                    .ToArray();
                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        row[columnNames[i]] = reader.IsDBNull(i)
                            ? null
                            : reader.GetValue(i);
                    }

                    rows.Add(row);
                }
            }

            return new DatabaseRowsResult(rows, total);
        }

        /// <inheritdoc />
        public async Task InsertRowAsync(
            string tableName,
            IReadOnlyDictionary<string, JsonElement> values,
            CancellationToken cancellationToken
        )
        {
            if (values is null)
            {
                throw new ArgumentException("No values supplied.", nameof(values));
            }

            using var connection = await OpenAsync(cancellationToken);
            await EnsureTableAsync(connection, tableName, cancellationToken);
            var schema = await GetSchemaInnerAsync(connection, tableName, cancellationToken);

            var allowed = schema.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            var columns = values.Keys
                .Where(IsValidIdentifier)
                .Where(allowed.Contains)
                .ToList();
            if (columns.Count == 0)
            {
                throw new ArgumentException("No valid columns supplied.", nameof(values));
            }

            var columnList = string.Join(", ", columns.Select(Quote));
            var paramList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"INSERT INTO {Quote(tableName)} ({columnList}) VALUES ({paramList});";
            for (var i = 0; i < columns.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", ToDbValue(values[columns[i]]));
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task UpdateRowAsync(
            string tableName,
            string primaryKeyValue,
            IReadOnlyDictionary<string, JsonElement> values,
            CancellationToken cancellationToken
        )
        {
            if (values is null)
            {
                throw new ArgumentException("No values supplied.", nameof(values));
            }

            using var connection = await OpenAsync(cancellationToken);
            await EnsureTableAsync(connection, tableName, cancellationToken);
            var schema = await GetSchemaInnerAsync(connection, tableName, cancellationToken);

            var pk = schema.PrimaryKeyColumn;
            if (!IsValidIdentifier(pk))
            {
                throw new ArgumentException("Invalid primary key column.", nameof(tableName));
            }

            var allowed = schema.Columns.Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
            var columns = values.Keys
                .Where(IsValidIdentifier)
                .Where(allowed.Contains)
                .Where(c => c != pk)
                .ToList();
            if (columns.Count == 0)
            {
                throw new ArgumentException("No updatable values supplied.", nameof(values));
            }

            var setClause = string.Join(
                ", ",
                columns.Select((c, i) => $"{Quote(c)} = @p{i}")
            );
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"UPDATE {Quote(tableName)} SET {setClause} WHERE {Quote(pk)} = @pk;";
            for (var i = 0; i < columns.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", ToDbValue(values[columns[i]]));
            }

            cmd.Parameters.AddWithValue("@pk", primaryKeyValue);
            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new RowNotFoundException(tableName, primaryKeyValue);
            }
        }

        /// <inheritdoc />
        public async Task DeleteRowAsync(
            string tableName,
            string primaryKeyValue,
            CancellationToken cancellationToken
        )
        {
            using var connection = await OpenAsync(cancellationToken);
            await EnsureTableAsync(connection, tableName, cancellationToken);
            var schema = await GetSchemaInnerAsync(connection, tableName, cancellationToken);

            var pk = schema.PrimaryKeyColumn;
            if (!IsValidIdentifier(pk))
            {
                throw new ArgumentException("Invalid primary key column.", nameof(tableName));
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"DELETE FROM {Quote(tableName)} WHERE {Quote(pk)} = @pk;";
            cmd.Parameters.AddWithValue("@pk", primaryKeyValue);
            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                throw new RowNotFoundException(tableName, primaryKeyValue);
            }
        }

        private static object? ToDbValue(JsonElement element) =>
            element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => 1,
                JsonValueKind.False => 0,
                JsonValueKind.Number when element.TryGetInt64(out var l) => l,
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.String => element.GetString(),
                _ => element.GetRawText(),
            };
    }
}