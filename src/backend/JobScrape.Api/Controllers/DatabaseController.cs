using JobScrape.Application.Abstractions;
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
        public async Task<IActionResult> ListTablesAsync(
            CancellationToken cancellationToken
        )
        {
            var tables = await _explorer.ListTablesAsync(cancellationToken);
            return Ok(new { items = tables });
        }

        /// <summary>Gets schema.</summary>
        [HttpGet("tables/{table}/schema")]
        public async Task<IActionResult> SchemaAsync(
            string table,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var schema = await _explorer.GetSchemaAsync(table, cancellationToken);
                return schema is null ? NotFound() : Ok(schema);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { code = "INVALID_TABLE", detail = ex.Message });
            }
        }

        /// <summary>Lists rows.</summary>
        [HttpGet("tables/{table}/rows")]
        public async Task<IActionResult> RowsAsync(
            string table,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                var result = await _explorer.ListRowsAsync(table, offset, limit, cancellationToken);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { code = "INVALID_TABLE", detail = ex.Message });
            }
        }

        /// <summary>Inserts a row.</summary>
        [HttpPost("tables/{table}/rows")]
        public async Task<IActionResult> InsertAsync(
            string table,
            [FromBody] Dictionary<string, System.Text.Json.JsonElement> values,
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
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { code = "INVALID_INPUT", detail = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                _ = ex;
                return BadRequest(new { code = "DB_ERROR", detail = "The database operation failed." });
            }
        }

        /// <summary>Updates a row.</summary>
        [HttpPut("tables/{table}/rows")]
        public async Task<IActionResult> UpdateAsync(
            string table,
            [FromBody] UpdateRowRequest request,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await _explorer.UpdateRowAsync(
                    table,
                    request.PrimaryKeyValue,
                    request.Values ?? new Dictionary<string, System.Text.Json.JsonElement>(),
                    cancellationToken
                );
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { code = "INVALID_INPUT", detail = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                _ = ex;
                return BadRequest(new { code = "DB_ERROR", detail = "The database operation failed." });
            }
        }

        /// <summary>Deletes a row.</summary>
        [HttpDelete("tables/{table}/rows/{primaryKeyValue}")]
        public async Task<IActionResult> DeleteAsync(
            string table,
            string primaryKeyValue,
            CancellationToken cancellationToken
        )
        {
            try
            {
                await _explorer.DeleteRowAsync(table, primaryKeyValue, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { code = "INVALID_INPUT", detail = ex.Message });
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                _ = ex;
                return BadRequest(new { code = "DB_ERROR", detail = "The database operation failed." });
            }
        }
    }

    /// <summary>Request body for updating a row.</summary>
    public sealed record UpdateRowRequest(string PrimaryKeyValue, Dictionary<string, System.Text.Json.JsonElement> Values);
}