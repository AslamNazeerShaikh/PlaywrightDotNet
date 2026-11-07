using System.Text.Json;
using Dapper;
using JobScrape.Application.Abstractions;
using Microsoft.Data.Sqlite;

namespace JobScrape.Infrastructure.Persistence
{
    /// <summary>
    /// Dapper-backed <see cref="IDatabaseExplorer"/> over the SQLite file.
    /// Table/column identifiers are validated against the live schema and
    /// quoted; values are always parameterized. Identity tables
    /// (<c>users</c>, <c>roles</c>) are never exposed, so password hashes
    /// cannot be read back through this API.
    /// </summary>
    public sealed class SqliteDatabaseExplorer(string connectionString) : IDatabaseExplorer
    {
        private readonly string _connectionString = connectionString;

        /// <summary>
        /// Tables the explorer never lists, describes, or touches.
        /// </summary>
        private static readonly HashSet<string> BlockedTables = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "users",
            "roles",
            "idempotency_keys",
        };

        private static bool IsValidIdentifier(string name) =>
            !string.IsNullOrWhiteSpace(name)
            && System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");

        private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

        private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
        {
            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync(ct);
                await connection.ExecuteAsync(
                    new CommandDefinition("PRAGMA foreign_keys = ON;", cancellationToken: ct)
                );
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
            var names = await connection.QueryAsync<string>(
                new CommandDefinition(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;",
                    cancellationToken: ct
                )
            );
            return new HashSet<string>(names, StringComparer.Ordinal);
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

            if (BlockedTables.Contains(tableName))
            {
                throw new TableNotFoundException(tableName);
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
                if (!IsValidIdentifier(table) || BlockedTables.Contains(table))
                {
                    continue;
                }

                var count = await connection.QuerySingleAsync<long>(
                    new CommandDefinition(
                        $"SELECT COUNT(*) FROM {Quote(table)};",
                        cancellationToken: cancellationToken
                    )
                );

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
            var columns = await ReadColumnsAsync(connection, tableName, cancellationToken);
            var foreignKeys = await ReadForeignKeysAsync(connection, tableName, cancellationToken);
            var pkColumn =
                columns.FirstOrDefault(c => c.PrimaryKey)?.Name
                ?? (columns.Count > 0 ? columns[0].Name : string.Empty);

            return new DatabaseTableSchema(tableName, columns, foreignKeys, pkColumn);
        }

        private static async Task<List<DatabaseColumnInfo>> ReadColumnsAsync(
            SqliteConnection connection,
            string tableName,
            CancellationToken cancellationToken
        )
        {
            var pragmaRows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(
                    $"PRAGMA table_info({Quote(tableName)});",
                    cancellationToken: cancellationToken
                )
            );
            var columns = new List<DatabaseColumnInfo>();
            foreach (var row in pragmaRows)
            {
                var values = (IDictionary<string, object?>)row;
                columns.Add(
                    new DatabaseColumnInfo(
                        GetString(values, "name") ?? string.Empty,
                        GetString(values, "type") ?? string.Empty,
                        GetInt(values, "notnull") == 1,
                        GetInt(values, "pk") != 0,
                        GetString(values, "dflt_value")
                    )
                );
            }

            return columns;
        }

        private static async Task<List<DatabaseForeignKeyInfo>> ReadForeignKeysAsync(
            SqliteConnection connection,
            string tableName,
            CancellationToken cancellationToken
        )
        {
            var fkRows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(
                    $"PRAGMA foreign_key_list({Quote(tableName)});",
                    cancellationToken: cancellationToken
                )
            );
            var foreignKeys = new List<DatabaseForeignKeyInfo>();
            foreach (var row in fkRows)
            {
                var values = (IDictionary<string, object?>)row;
                foreignKeys.Add(
                    new DatabaseForeignKeyInfo(
                        GetString(values, "from") ?? string.Empty,
                        GetString(values, "table") ?? string.Empty,
                        GetString(values, "to") ?? string.Empty
                    )
                );
            }

            return foreignKeys;
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

            var total = await connection.QuerySingleAsync<long>(
                new CommandDefinition(
                    $"SELECT COUNT(*) FROM {Quote(tableName)};",
                    cancellationToken: cancellationToken
                )
            );

            var parameters = new DynamicParameters();
            parameters.Add("limit", limit);
            parameters.Add("offset", offset);
            var dataRows = await connection.QueryAsync<dynamic>(
                new CommandDefinition(
                    $"SELECT * FROM {Quote(tableName)} LIMIT @limit OFFSET @offset;",
                    parameters,
                    cancellationToken: cancellationToken
                )
            );

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            foreach (var dataRow in dataRows)
            {
                var values = (IDictionary<string, object?>)dataRow;
                var row = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var pair in values)
                {
                    row[pair.Key] = pair.Value is DBNull ? null : JsonSafe(pair.Value);
                }

                rows.Add(row);
            }

            return new DatabaseRowsResult(rows, total);
        }

        // JSON numbers above 2^53 lose precision in JavaScript. Return those as strings so the
        // cockpit can render .NET ticks and other large integers exactly.
        private const long MaxJsonSafeInteger = 9_007_199_254_740_991L;

        private static object? JsonSafe(object? value) =>
            value is long number && (number > MaxJsonSafeInteger || number < -MaxJsonSafeInteger)
                ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : value;

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
            var columns = values.Keys.Where(IsValidIdentifier).Where(allowed.Contains).ToList();
            if (columns.Count == 0)
            {
                throw new ArgumentException("No valid columns supplied.", nameof(values));
            }

            var columnList = string.Join(", ", columns.Select(Quote));
            var paramList = string.Join(", ", columns.Select((_, i) => $"@p{i}"));
            var parameters = ValueParameters(columns, values);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    $"INSERT INTO {Quote(tableName)} ({columnList}) VALUES ({paramList});",
                    parameters,
                    cancellationToken: cancellationToken
                )
            );
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
            var columns = values
                .Keys.Where(IsValidIdentifier)
                .Where(allowed.Contains)
                .Where(c => c != pk)
                .ToList();
            if (columns.Count == 0)
            {
                throw new ArgumentException("No updatable values supplied.", nameof(values));
            }

            var setClause = string.Join(", ", columns.Select((c, i) => $"{Quote(c)} = @p{i}"));
            var parameters = ValueParameters(columns, values);

            parameters.Add("pk", primaryKeyValue);
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(
                    $"UPDATE {Quote(tableName)} SET {setClause} WHERE {Quote(pk)} = @pk;",
                    parameters,
                    cancellationToken: cancellationToken
                )
            );
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

            var parameters = new DynamicParameters();
            parameters.Add("pk", primaryKeyValue);
            var affected = await connection.ExecuteAsync(
                new CommandDefinition(
                    $"DELETE FROM {Quote(tableName)} WHERE {Quote(pk)} = @pk;",
                    parameters,
                    cancellationToken: cancellationToken
                )
            );
            if (affected == 0)
            {
                throw new RowNotFoundException(tableName, primaryKeyValue);
            }
        }

        private static DynamicParameters ValueParameters(
            List<string> columns,
            IReadOnlyDictionary<string, JsonElement> values
        )
        {
            var parameters = new DynamicParameters();
            for (var i = 0; i < columns.Count; i++)
            {
                parameters.Add($"p{i}", ToDbValue(values[columns[i]]));
            }

            return parameters;
        }

        private static object? GetRaw(IDictionary<string, object?> values, string key)
        {
            foreach (var pair in values)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value is DBNull ? null : pair.Value;
                }
            }

            return null;
        }

        private static string? GetString(IDictionary<string, object?> values, string key) =>
            GetRaw(values, key)?.ToString();

        private static long GetInt(IDictionary<string, object?> values, string key)
        {
            var raw = GetRaw(values, key);
            return raw is null
                ? 0
                : Convert.ToInt64(raw, System.Globalization.CultureInfo.InvariantCulture);
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
