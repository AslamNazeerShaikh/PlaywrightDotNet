using JobScrape.Application.Results;
using ModelContextProtocol;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// Maps expected handler failures to MCP tool errors. The installed
    /// <c>ModelContextProtocol</c> 2.3.1 <see cref="McpException"/> carries
    /// only a message, so the stable <see cref="ErrorInfo.Code"/> travels
    /// inside it ahead of the operator-safe detail.
    /// </summary>
    internal static class McpErrors
    {
        /// <summary>
        /// Builds the tool error for an expected handler failure.
        /// </summary>
        /// <param name="error">The handler failure. Must not be null.</param>
        /// <returns>An exception whose message is the code plus the detail.</returns>
        internal static McpException From(ErrorInfo error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new McpException($"{error.Code}: {error.Detail}");
        }

        /// <summary>
        /// Returns the value on success, or throws the mapped tool error.
        /// </summary>
        /// <typeparam name="T">The success value type.</typeparam>
        /// <param name="result">The handler outcome.</param>
        /// <returns>The success value.</returns>
        internal static T OrThrow<T>(Result<T> result)
        {
            return result.IsSuccess ? result.Value : throw From(result.Error);
        }

        /// <summary>
        /// Parses a tool id argument, throwing a validation tool error when
        /// the text is not a GUID. Empty GUIDs pass through: the handler owns
        /// the empty-id rule and reports it with the same code.
        /// </summary>
        /// <param name="id">The raw id argument. May be null.</param>
        /// <param name="requiredMessage">The field message when parsing fails.</param>
        /// <returns>The parsed identifier.</returns>
        internal static Guid ParseIdOrThrow(string? id, string requiredMessage)
        {
            if (Guid.TryParse(id, out var guid))
            {
                return guid;
            }

            throw From(
                Errors.ValidationFailed(
                    "The request contains invalid fields.",
                    new Dictionary<string, string[]> { ["id"] = [requiredMessage] }
                )
            );
        }
    }
}
