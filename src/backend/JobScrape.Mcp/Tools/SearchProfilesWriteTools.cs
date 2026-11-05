using System.ComponentModel;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Features.SearchProfiles;
using ModelContextProtocol.Server;

namespace JobScrape.Mcp.Tools
{
    /// <summary>
    /// The search profile write tools. The source is immutable: creation
    /// takes it, updates do not. Expected handler failures surface as tool
    /// errors carrying the failure code.
    /// </summary>
    [McpServerToolType]
    public sealed class SearchProfilesWriteTools
    {
        private readonly CreateSearchProfileHandler _create;
        private readonly UpdateSearchProfileHandler _update;

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchProfilesWriteTools"/> class.
        /// </summary>
        /// <param name="create">The profile creator. Must not be null.</param>
        /// <param name="update">The profile updater. Must not be null.</param>
        /// <param name="caller">
        /// The calling actor. Writes ignore it today; the parameter exists
        /// so future authorization needs no signature change. Must not be null.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        public SearchProfilesWriteTools(
            CreateSearchProfileHandler create,
            UpdateSearchProfileHandler update,
            ICallerContext caller
        )
        {
            ArgumentNullException.ThrowIfNull(create);
            ArgumentNullException.ThrowIfNull(update);
            ArgumentNullException.ThrowIfNull(caller);
            _create = create;
            _update = update;
        }

        /// <summary>
        /// Creates a search profile with the source's policy caps by default.
        /// </summary>
        /// <param name="name">Unique profile name.</param>
        /// <param name="sourceKey">Source key: <c>naukri</c>, <c>indeed</c>, or <c>linkedin</c>.</param>
        /// <param name="keywords">Search keywords as the source query expects them.</param>
        /// <param name="locations">Cities or regions; may be empty when remote-only.</param>
        /// <param name="experienceYears">Optional years of experience, 0-50.</param>
        /// <param name="postedWithinDays">Optional recency window, 1-30 days.</param>
        /// <param name="remoteOnly">True to search remote roles only.</param>
        /// <param name="maxPages">Optional page cap, up to the source default.</param>
        /// <param name="startPage">Optional first page, 1 and up; defaults to 1.</param>
        /// <param name="maxJobs">Optional job cap, up to the source default.</param>
        /// <param name="maxDetails">Optional detail cap, up to the source default.</param>
        /// <param name="minDelayMs">Optional shortest delay, at least the source default.</param>
        /// <param name="maxDelayMs">Optional longest delay, at least the source default.</param>
        /// <param name="requiredSkills">Optional key skills; a posting is kept when any one matches.</param>
        /// <param name="companies">Optional company names for the include and exclude exports.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The saved profile.</returns>
        [McpServerTool(Name = "create_profile")]
        [Description("Creates a search profile with the source policy caps by default.")]
        public async Task<SearchProfileResponse> CreateProfileAsync(
            [Description("Unique profile name.")] string? name = null,
            [Description("Source key: naukri, indeed, or linkedin.")] string? sourceKey = null,
            [Description("Search keywords as the source query expects them.")]
                string? keywords = null,
            [Description("Cities or regions; may be empty when remote-only.")]
                string[]? locations = null,
            [Description("Optional years of experience, 0 to 50.")] int? experienceYears = null,
            [Description("Optional recency window, 1 to 30 days.")] int? postedWithinDays = null,
            [Description("True to search remote roles only.")] bool remoteOnly = false,
            [Description("Optional page cap, up to the source default.")] int? maxPages = null,
            [Description("Optional first page, 1 and up; defaults to 1.")] int? startPage = null,
            [Description("Optional job cap, up to the source default.")] int? maxJobs = null,
            [Description("Optional detail cap, up to the source default.")] int? maxDetails = null,
            [Description("Optional shortest delay, at least the source default.")]
                int? minDelayMs = null,
            [Description("Optional longest delay, at least the source default.")]
                int? maxDelayMs = null,
            [Description("Optional key skills; a posting is kept when any one matches.")]
                string[]? requiredSkills = null,
            [Description("Optional company names for the include and exclude exports.")]
                string[]? companies = null,
            CancellationToken cancellationToken = default
        )
        {
            return McpErrors.OrThrow(
                await _create.HandleAsync(
                    new CreateSearchProfileRequest(
                        name,
                        sourceKey,
                        keywords,
                        locations,
                        experienceYears,
                        postedWithinDays,
                        remoteOnly,
                        maxPages,
                        startPage,
                        maxJobs,
                        maxDetails,
                        minDelayMs,
                        maxDelayMs,
                        requiredSkills,
                        companies
                    ),
                    cancellationToken
                )
            );
        }

        /// <summary>
        /// Replaces a stored search profile's editable fields. The source is
        /// immutable and is not part of this call.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="enabled">Whether new runs may start.</param>
        /// <param name="name">Unique profile name.</param>
        /// <param name="keywords">Search keywords as the source query expects them.</param>
        /// <param name="locations">Cities or regions; may be empty when remote-only.</param>
        /// <param name="experienceYears">Optional years of experience, 0-50.</param>
        /// <param name="postedWithinDays">Optional recency window, 1-30 days.</param>
        /// <param name="remoteOnly">True to search remote roles only.</param>
        /// <param name="maxPages">Page cap, up to the source default.</param>
        /// <param name="startPage">First page fetched, 1 and up.</param>
        /// <param name="maxJobs">Job cap, up to the source default.</param>
        /// <param name="maxDetails">Detail cap, up to the source default.</param>
        /// <param name="minDelayMs">Shortest delay, at least the source default.</param>
        /// <param name="maxDelayMs">Longest delay, at least the source default.</param>
        /// <param name="requiredSkills">Optional key skills; a posting is kept when any one matches.</param>
        /// <param name="companies">Optional company names for the include and exclude exports.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The saved profile.</returns>
        [McpServerTool(Name = "update_profile")]
        [Description(
            "Replaces a stored search profile's editable fields. The source stays unchanged."
        )]
        public async Task<SearchProfileResponse> UpdateProfileAsync(
            [Description("The profile identifier.")] string id,
            [Description("Whether new runs may start.")] bool enabled,
            [Description("Unique profile name.")] string? name = null,
            [Description("Search keywords as the source query expects them.")]
                string? keywords = null,
            [Description("Cities or regions; may be empty when remote-only.")]
                string[]? locations = null,
            [Description("Optional years of experience, 0 to 50.")] int? experienceYears = null,
            [Description("Optional recency window, 1 to 30 days.")] int? postedWithinDays = null,
            [Description("True to search remote roles only.")] bool remoteOnly = false,
            [Description("Page cap, up to the source default.")] int? maxPages = null,
            [Description("First page fetched, 1 and up.")] int? startPage = null,
            [Description("Job cap, up to the source default.")] int? maxJobs = null,
            [Description("Detail cap, up to the source default.")] int? maxDetails = null,
            [Description("Shortest delay, at least the source default.")] int? minDelayMs = null,
            [Description("Longest delay, at least the source default.")] int? maxDelayMs = null,
            [Description("Optional key skills; a posting is kept when any one matches.")]
                string[]? requiredSkills = null,
            [Description("Optional company names for the include and exclude exports.")]
                string[]? companies = null,
            CancellationToken cancellationToken = default
        )
        {
            var profileId = McpErrors.ParseIdOrThrow(id, "A profile id is required.");
            return McpErrors.OrThrow(
                await _update.HandleAsync(
                    profileId,
                    new UpdateSearchProfileRequest(
                        name,
                        keywords,
                        locations,
                        experienceYears,
                        postedWithinDays,
                        remoteOnly,
                        maxPages,
                        startPage,
                        maxJobs,
                        maxDetails,
                        minDelayMs,
                        maxDelayMs,
                        requiredSkills,
                        companies,
                        enabled
                    ),
                    cancellationToken
                )
            );
        }
    }
}
