using System.Text.Json;

namespace JobScrape.Application.Abstractions
{
    /// <summary>
    /// Read/write access to the raw SQLite database for the cockpit's
    /// database explorer. Table and column names are validated against the
    /// live schema; values are parameterized. Only user tables are exposed.
    /// </summary>
    public interface IDatabaseExplorer
    {
        /// <summary>Lists user tables with row counts.</summary>
        Task<IReadOnlyList<DatabaseTableInfo>> ListTablesAsync(
            CancellationToken cancellationToken
        );

        /// <summary>Gets column and foreign-key metadata for a table.</summary>
        Task<DatabaseTableSchema?> GetSchemaAsync(
            string tableName,
            CancellationToken cancellationToken
        );

        /// <summary>Lists a page of rows for a table.</summary>
        Task<DatabaseRowsResult> ListRowsAsync(
            string tableName,
            int offset,
            int limit,
            CancellationToken cancellationToken
        );

        /// <summary>Inserts a row from a column-to-value map.</summary>
        Task InsertRowAsync(
            string tableName,
            IReadOnlyDictionary<string, JsonElement> values,
            CancellationToken cancellationToken
        );

        /// <summary>Updates a row identified by its primary key.</summary>
        Task UpdateRowAsync(
            string tableName,
            string primaryKeyValue,
            IReadOnlyDictionary<string, JsonElement> values,
            CancellationToken cancellationToken
        );

        /// <summary>Deletes a row identified by its primary key.</summary>
        Task DeleteRowAsync(
            string tableName,
            string primaryKeyValue,
            CancellationToken cancellationToken
        );
    }

    /// <summary>
    /// Summary of one database table.
    /// </summary>
    public sealed record DatabaseTableInfo(string Name, long RowCount);
    /// <summary>
    /// Column metadata for one table.
    /// </summary>
    public sealed record DatabaseColumnInfo(
        string Name,
        string Type,
        bool NotNull,
        bool PrimaryKey,
        string? DefaultValue
    );

    /// <summary>
    /// Foreign key metadata for one table.
    /// </summary>
    public sealed record DatabaseForeignKeyInfo(
        string Column,
        string ReferencedTable,
        string ReferencedColumn
    );

    /// <summary>
    /// Full schema for one table.
    /// </summary>
    public sealed record DatabaseTableSchema(
        string Name,
        IReadOnlyList<DatabaseColumnInfo> Columns,
        IReadOnlyList<DatabaseForeignKeyInfo> ForeignKeys,
        string PrimaryKeyColumn
    );

    /// <summary>
    /// A page of rows plus the total count.
    /// </summary>
    public sealed record DatabaseRowsResult(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
        long Total
    );

    /// <summary>
    /// Thrown when a database explorer operation names an unknown table.
    /// </summary>
    public sealed class TableNotFoundException(string tableName)
        : KeyNotFoundException($"Table '{tableName}' was not found.")
    {
        /// <summary>Gets the unknown table name.</summary>
        public string TableName { get; } = tableName;
    }

    /// <summary>
    /// Thrown when a database explorer operation names an unknown row.
    /// </summary>
    public sealed class RowNotFoundException(string tableName, string primaryKeyValue)
        : KeyNotFoundException($"Row '{primaryKeyValue}' was not found.")
    {
        /// <summary>Gets the table that was searched.</summary>
        public string TableName { get; } = tableName;

        /// <summary>Gets the requested primary key value.</summary>
        public string PrimaryKeyValue { get; } = primaryKeyValue;
    }
}