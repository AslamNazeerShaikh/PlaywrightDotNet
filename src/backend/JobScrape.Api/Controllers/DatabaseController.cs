using JobScrape.Api.Http;
using JobScrape.Api.Http.Auth;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobScrape.Api.Controllers
{
    /// <summary>
    /// Database explorer: list tables, inspect schema, browse rows, and
    /// perform CRUD on allow-listed tables (docs/12-web-cockpit.md). Admin
    /// role only; identity tables are never listed.
    /// </summary>
    [ApiController]
    [Route("api/v1/db")]
    [Authorize(Policy = AuthPolicies.RequireAdmin)]
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
        public Task<IResult> SchemaAsync(string table, CancellationToken cancellationToken)
        {
            return GuardedAsync(
                table,
                async () =>
                {
                    var schema = await _explorer.GetSchemaAsync(table, cancellationToken);
                    return schema is null
                        ? Errors.TableNotFound(table).ToProblem()
                        : Results.Ok(schema);
                },
                writes: false
            );
        }

        /// <summary>Lists rows.</summary>
        [HttpGet("tables/{table}/rows")]
        public Task<IResult> RowsAsync(
            string table,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default
        )
        {
            return GuardedAsync(
                table,
                async () =>
                    Results.Ok(
                        await _explorer.ListRowsAsync(table, offset, limit, cancellationToken)
                    ),
                writes: false
            );
        }

        /// <summary>Inserts a row.</summary>
        [HttpPost("tables/{table}/rows")]
        public Task<IResult> InsertAsync(
            string table,
            [FromBody] Dictionary<string, System.Text.Json.JsonElement>? values,
            CancellationToken cancellationToken
        )
        {
            return GuardedAsync(
                table,
                async () =>
                {
                    await _explorer.InsertRowAsync(
                        table,
                        values ?? new Dictionary<string, System.Text.Json.JsonElement>(),
                        cancellationToken
                    );
                    return Results.NoContent();
                },
                writes: true
            );
        }

        /// <summary>Updates a row.</summary>
        [HttpPut("tables/{table}/rows")]
        public Task<IResult> UpdateAsync(
            string table,
            [FromBody] UpdateRowRequest? request,
            CancellationToken cancellationToken
        )
        {
            var primaryKeyValue = request?.PrimaryKeyValue;
            if (string.IsNullOrWhiteSpace(primaryKeyValue))
            {
                return Task.FromResult(
                    Errors.InvalidInput("PrimaryKeyValue is required.").ToProblem()
                );
            }

            return GuardedAsync(
                table,
                async () =>
                {
                    await _explorer.UpdateRowAsync(
                        table,
                        primaryKeyValue,
                        request!.Values ?? new Dictionary<string, System.Text.Json.JsonElement>(),
                        cancellationToken
                    );
                    return Results.NoContent();
                },
                writes: true
            );
        }

        /// <summary>Deletes a row.</summary>
        [HttpDelete("tables/{table}/rows/{primaryKeyValue}")]
        public Task<IResult> DeleteAsync(
            string table,
            string primaryKeyValue,
            CancellationToken cancellationToken
        )
        {
            return GuardedAsync(
                table,
                async () =>
                {
                    await _explorer.DeleteRowAsync(table, primaryKeyValue, cancellationToken);
                    return Results.NoContent();
                },
                writes: true
            );
        }

        /// <summary>
        /// Runs one explorer call and maps the port's expected failures. Reads
        /// report an invalid table name as <c>INVALID_TABLE</c>; writes report
        /// it as <c>INVALID_INPUT</c>, because the same guard also rejects a
        /// body with no usable column.
        /// </summary>
        /// <param name="table">The requested table, echoed in the problem.</param>
        /// <param name="action">The explorer call.</param>
        /// <param name="writes">True for insert, update, and delete.</param>
        /// <returns>The action's result, or the mapped problem.</returns>
        private static async Task<IResult> GuardedAsync(
            string table,
            Func<Task<IResult>> action,
            bool writes
        )
        {
            try
            {
                return await action();
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
                return writes
                    ? Errors.InvalidInput(ex.Message).ToProblem()
                    : Errors.InvalidTable(ex.Message).ToProblem();
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
