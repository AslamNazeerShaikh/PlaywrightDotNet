using JobScrape.Api.Http;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Database explorer: list tables, inspect schema, browse rows, and
    /// perform CRUD on allow-listed tables (docs/12-web-cockpit.md).
    /// </summary>
    [ApiController]
    [Route("api/v1/db")]
    public sealed class DatabaseController : ControllerBase
    {
        private readonly IDatabaseExplorer _explorer;

        /// <summary>Initializes a new instance.</summary>
        public DatabaseController(IDatabaseExplorer explorer)
        {
            ArgumentNullException.ThrowIfNull(explorer);
            _explorer = explorer;
        }

        /// <summary>Lists tables.</summary>
        [HttpGet("tables")]
        public async Task<IResult> ListTablesAsync(CancellationToken cancellationToken)
        {
            var tables = await _explorer.ListTablesAsync(cancellationToken);
            return Results.Ok(new { items = tables });
        }

        /// <summary>Gets schema.</summary>
        [HttpGet("tables/{table}/schema")]
        public async Task<IResult> SchemaAsync(string table, CancellationToken cancellationToken)
        {
            try
            {
                var schema = await _explorer.GetSchemaAsync(table, cancellationToken);
                return schema is null
                    ? Errors.TableNotFound(table).ToProblem()
                    : Results.Ok(schema);
            }
            catch (TableNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (KeyNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (ArgumentException ex)
            {
                return Errors.InvalidTable(ex.Message).ToProblem();
            }
        }

        /// <summary>Lists rows.</summary>
        [HttpGet("tables/{table}/rows")]
        public async Task<IResult> RowsAsync(
            string table,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var result = await _explorer.ListRowsAsync(table, offset, limit, cancellationToken);
                return Results.Ok(result);
            }
            catch (TableNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (KeyNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (ArgumentException ex)
            {
                return Errors.InvalidTable(ex.Message).ToProblem();
            }
        }

        /// <summary>Inserts a row.</summary>
        [HttpPost("tables/{table}/rows")]
        public async Task<IResult> InsertAsync(
            string table,
            [FromBody] Dictionary<string, System.Text.Json.JsonElement>? values,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await _explorer.InsertRowAsync(
                    table,
                    values ?? new Dictionary<string, System.Text.Json.JsonElement>(),
                    cancellationToken
                );
                return Results.NoContent();
            }
            catch (TableNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (KeyNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (ArgumentException ex)
            {
                return Errors.InvalidInput(ex.Message).ToProblem();
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return Errors.DatabaseError().ToProblem();
            }
        }

        /// <summary>Updates a row.</summary>
        [HttpPut("tables/{table}/rows")]
        public async Task<IResult> UpdateAsync(
            string table,
            [FromBody] UpdateRowRequest? request,
            CancellationToken cancellationToken
        )
        {
            var primaryKeyValue = request?.PrimaryKeyValue;
            if (string.IsNullOrWhiteSpace(primaryKeyValue))
            {
                return Errors.InvalidInput("PrimaryKeyValue is required.").ToProblem();
            }

            try
            {
                await _explorer.UpdateRowAsync(
                    table,
                    primaryKeyValue,
                    request!.Values ?? new Dictionary<string, System.Text.Json.JsonElement>(),
                    cancellationToken
                );
                return Results.NoContent();
            }
            catch (RowNotFoundException ex)
            {
                return Errors.TableRowNotFound(ex.TableName, ex.PrimaryKeyValue).ToProblem();
            }
            catch (TableNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (KeyNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (ArgumentException ex)
            {
                return Errors.InvalidInput(ex.Message).ToProblem();
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return Errors.DatabaseError().ToProblem();
            }
        }

        /// <summary>Deletes a row.</summary>
        [HttpDelete("tables/{table}/rows/{primaryKeyValue}")]
        public async Task<IResult> DeleteAsync(
            string table,
            string primaryKeyValue,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await _explorer.DeleteRowAsync(table, primaryKeyValue, cancellationToken);
                return Results.NoContent();
            }
            catch (RowNotFoundException ex)
            {
                return Errors.TableRowNotFound(ex.TableName, ex.PrimaryKeyValue).ToProblem();
            }
            catch (TableNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (KeyNotFoundException)
            {
                return Errors.TableNotFound(table).ToProblem();
            }
            catch (ArgumentException ex)
            {
                return Errors.InvalidInput(ex.Message).ToProblem();
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return Errors.DatabaseError().ToProblem();
            }
        }
    }

    /// <summary>Request body for updating a row.</summary>
    public sealed record UpdateRowRequest(
        string PrimaryKeyValue,
        Dictionary<string, System.Text.Json.JsonElement> Values
    );
}
