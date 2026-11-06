using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.SearchProfiles;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The search profile tools. Read-only; single-profile reads report
    /// <c>VALIDATION_FAILED</c> and <c>PROFILE_NOT_FOUND</c> as tool errors.
    /// </summary>
    [McpServerToolType]
    public sealed class SearchProfilesTools
    {
        private readonly ListSearchProfilesHandler _list;
        private readonly GetSearchProfileHandler _get;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchProfilesTools"/> class.
        /// </summary>
        /// <param name="list">The profile list handler. Must not be null.</param>
        /// <param name="get">The single-profile handler. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Read tools ignore it; the parameter exists so
        /// future authorization needs no signature change. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SearchProfilesTools(
            ListSearchProfilesHandler list,
            GetSearchProfileHandler get,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(list);
            ArgumentNullException.ThrowIfNull(get);
            ArgumentNullException.ThrowIfNull(caller);
            _list = list;
            _get = get;
        }

        /// <summary>
        /// Returns every profile ordered by name.
        /// </summary>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile list.</returns>
        [McpServerTool(Name = "list_profiles")]
        [Description("Returns every saved search profile ordered by name.")]
        public Task<SearchProfileListResponse> ListProfilesAsync(
            CancellationToken cancellationToken = default
        )
        {
            return _list.HandleAsync(cancellationToken);
        }

        /// <summary>
        /// Returns the profile summary for one id.
        /// </summary>
        /// <param name="id">The requested profile identifier.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The profile summary.</returns>
        [McpServerTool(Name = "get_profile")]
        [Description("Returns one saved search profile by its identifier.")]
        public async Task<SearchProfileResponse> GetProfileAsync(
            [Description("The profile identifier.")] string id,
            CancellationToken cancellationToken = default
        )
        {
            var profileId = McpErrors.ParseIdOrThrow(id, "A profile id is required.");
            return McpErrors.OrThrow(await _get.HandleAsync(profileId, cancellationToken));
        }
    }
}
