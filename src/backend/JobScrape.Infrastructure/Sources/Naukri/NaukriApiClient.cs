using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using Microsoft.Extensions.Logging;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// One search row from the <c>jobapi</c> response: the snapshot the
    /// shared field helpers produced, plus whether the posting applies on a
    /// company portal instead of Naukri. External rows are never collected
    /// as ordinary postings.
    /// </summary>
    /// <param name="Snapshot">The mapped posting fields.</param>
    /// <param name="ExternalPortal">
    /// True when the row carries <c>companyApplyJob</c>, <c>clientCareersUrl</c>,
    /// or <c>applyRedirectUrl</c>.
    /// </param>
    public sealed record NaukriApiRow(JobSnapshot Snapshot, bool ExternalPortal);

    /// <summary>
    /// One fetched <c>jobapi/v3/search</c> page.
    /// </summary>
    /// <param name="Rows">The mapped rows; malformed rows are excluded and counted.</param>
    /// <param name="Total">The listing size reported by <c>noOfJobs</c>.</param>
    /// <param name="DroppedRows">Rows dropped as malformed.</param>
    /// <param name="EndOfListing">
    /// True when the API answered <c>400</c> past the last page or returned
    /// no rows: the listing has ended cleanly.
    /// </param>
    /// <param name="Challenge">The page classification; <see cref="ChallengeAssessment.Clear"/> for a usable page.</param>
    public sealed record NaukriApiSearchPage(
        IReadOnlyList<NaukriApiRow> Rows,
        int Total,
        int DroppedRows,
        bool EndOfListing,
        ChallengeAssessment Challenge
    );

    /// <summary>
    /// A bounded search over the <c>jobapi</c> pages: rows from every page
    /// fetched, de-duplicated by job id, and the classification that stopped
    /// the loop. A wall keeps the rows collected before it.
    /// </summary>
    /// <param name="Rows">The collected rows, at most the requested maximum.</param>
    /// <param name="Total">The largest listing size any page reported.</param>
    /// <param name="DroppedRows">Rows dropped as malformed across every page.</param>
    /// <param name="PagesFetched">How many requests the search made.</param>
    /// <param name="Challenge">The wall that stopped the search, or Clear when the listing ended.</param>
    public sealed record NaukriApiSearchResult(
        IReadOnlyList<NaukriApiRow> Rows,
        int Total,
        int DroppedRows,
        int PagesFetched,
        ChallengeAssessment Challenge
    );

    /// <summary>
    /// Fetches Naukri search listings from its own <c>jobapi</c> endpoint
    /// (docs/superpowers/specs/2026-11-18-naukri-api-fast-path.md). Requests
    /// carry the verified contract: <c>appid: 109</c>, <c>systemid: Naukri</c>,
    /// a fresh <c>nkparam</c> token per call, an honest Chrome user agent,
    /// and a 25-second timeout. The transport never follows redirects: a
    /// <c>3xx</c> answer is a wall, not a hop.
    /// <para>
    /// Status mapping: <c>200</c> parses rows through the shared
    /// <see cref="NaukriFields"/> helpers, <c>400</c> is a clean end,
    /// <c>406</c> is <see cref="ChallengeKind.RateLimited"/>, <c>3xx</c> is a
    /// wall, and every other non-200 is <see cref="ChallengeKind.TransientError"/>.
    /// Malformed rows drop with a warning and never poison the page.
    /// </para>
    /// </summary>
    public sealed partial class NaukriApiClient : IDisposable
    {
        /// <summary>
        /// The number of rows requested per page (<c>noOfResults</c>).
        /// </summary>
        public const int PageSize = 20;

        private const string SearchBaseUrl = "https://www.naukri.com/jobapi/v3/search";

        private const string UserAgent =
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(25);

        private readonly HttpClient _client;
        private readonly ILogger<NaukriApiClient> _logger;

        /// <summary>
        /// Initializes a new instance with the production transport: redirects
        /// disabled and a 25-second request timeout.
        /// </summary>
        /// <param name="logger">The logger. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
        public NaukriApiClient(ILogger<NaukriApiClient> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);
            _logger = logger;
            _client = new HttpClient(CreateTransportHandler()) { Timeout = RequestTimeout };
        }

        /// <summary>
        /// Initializes a new instance over a supplied client. The client owns
        /// and disposes it; callers must not reuse it.
        /// </summary>
        /// <param name="client">The HTTP client. Must not be null.</param>
        /// <param name="logger">The logger. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        internal NaukriApiClient(HttpClient client, ILogger<NaukriApiClient> logger)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(logger);
            _client = client;
            _logger = logger;
        }

        /// <summary>
        /// Fetches one search page. Every call mints a fresh token and never
        /// reuses a previous one.
        /// </summary>
        /// <param name="criteria">The validated search criteria. Must not be null.</param>
        /// <param name="pageNumber">The one-based page number.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>
        /// The mapped page, or a classified wall. A <c>400</c> is reported as
        /// a clean end; no status is thrown.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="pageNumber"/> is below 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<NaukriApiSearchPage> FetchPageAsync(
            SearchCriteria criteria,
            int pageNumber,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);

            var uri = SearchUri(criteria, pageNumber);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("appid", "109");
            request.Headers.TryAddWithoutValidation("systemid", "Naukri");
            request.Headers.TryAddWithoutValidation("nkparam", NaukriRequestToken.Generate("srp"));
            request.Headers.Accept.Add(new("application/json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                NaukriApiLog.Request(_logger, request);
            }

            HttpResponseMessage response;
            try
            {
                response = await _client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                LogRequestFailed(_logger, ex, pageNumber);
                return Wall(ChallengeKind.TransientError, 0.8, "network");
            }
            catch (OperationCanceledException ex)
            {
                LogRequestFailed(_logger, ex, pageNumber);
                return Wall(ChallengeKind.TransientError, 0.8, "timeout");
            }

            using (response)
            {
                switch ((int)response.StatusCode)
                {
                    case 200:
                    {
                        var body = await response.Content.ReadAsStringAsync(cancellationToken);
                        NaukriApiLog.Response(_logger, uri, 200, body);
                        return ParsePage(body, pageNumber, uri);
                    }
                    case 400:
                        NaukriApiLog.Outcome(_logger, uri, 400, "end-of-listing");
                        return new NaukriApiSearchPage([], 0, 0, true, ChallengeAssessment.Clear);
                    case 406:
                        return await RejectedAsync(
                            response,
                            uri,
                            406,
                            "rate-limited",
                            Wall(ChallengeKind.RateLimited, 0.95, "status=406"),
                            cancellationToken
                        );
                    case >= 300 and < 400:
                        return await RejectedAsync(
                            response,
                            uri,
                            (int)response.StatusCode,
                            "redirect",
                            Wall(
                                ChallengeKind.SoftChallenge,
                                0.9,
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"status={(int)response.StatusCode}"
                                )
                            ),
                            cancellationToken
                        );
                    default:
                        return await RejectedAsync(
                            response,
                            uri,
                            (int)response.StatusCode,
                            "transient",
                            Wall(
                                ChallengeKind.TransientError,
                                0.8,
                                string.Create(
                                    CultureInfo.InvariantCulture,
                                    $"status={(int)response.StatusCode}"
                                )
                            ),
                            cancellationToken
                        );
                }
            }
        }

        /// <summary>
        /// Reads and logs a rejected response's body, then returns the
        /// already-classified wall. Only the failure path may log body
        /// content, and only through the shared capped preview.
        /// </summary>
        /// <param name="response">The rejected response.</param>
        /// <param name="uri">The request URI, for the log line.</param>
        /// <param name="status">The response status.</param>
        /// <param name="reason">The classification, for the log line.</param>
        /// <param name="page">The pre-classified wall.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The wall.</returns>
        private async Task<NaukriApiSearchPage> RejectedAsync(
            HttpResponseMessage response,
            Uri uri,
            int status,
            string reason,
            NaukriApiSearchPage page,
            CancellationToken cancellationToken
        )
        {
            var body = await NaukriApiLog.RejectedBodyAsync(response, cancellationToken);
            NaukriApiLog.Failure(_logger, uri, status, reason, body);
            return page;
        }

        /// <summary>
        /// Fetches pages until the listing ends, <paramref name="maxJobs"/>
        /// rows are collected, a page is empty, or a wall stops the run. The
        /// loop is bounded by the reported total, so a full last page never
        /// starts another request.
        /// </summary>
        /// <param name="criteria">The validated search criteria. Must not be null.</param>
        /// <param name="maxJobs">The most rows to collect; at least 1.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The collected rows and the reason the loop stopped.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="criteria"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxJobs"/> is below 1.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<NaukriApiSearchResult> SearchAsync(
            SearchCriteria criteria,
            int maxJobs,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxJobs);

            var rows = new List<NaukriApiRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var total = 0;
            var dropped = 0;
            var pages = 0;
            for (var pageNumber = 1; ; pageNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await FetchPageAsync(criteria, pageNumber, cancellationToken);
                pages++;
                total = Math.Max(total, page.Total);
                dropped += page.DroppedRows;
                if (page.Challenge.Kind != ChallengeKind.None)
                {
                    return new NaukriApiSearchResult(rows, total, dropped, pages, page.Challenge);
                }

                foreach (var row in page.Rows)
                {
                    if (seen.Add(row.Snapshot.ExternalJobId))
                    {
                        rows.Add(row);
                    }
                }

                if (rows.Count >= maxJobs)
                {
                    rows.RemoveRange(maxJobs, rows.Count - maxJobs);
                    break;
                }

                if (page.EndOfListing || page.Rows.Count == 0 || pageNumber * PageSize >= total)
                {
                    break;
                }
            }

            return new NaukriApiSearchResult(
                rows,
                total,
                dropped,
                pages,
                ChallengeAssessment.Clear
            );
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Creates the production transport: redirects are never followed,
        /// compressed responses are accepted, and pooled connections rotate
        /// every two minutes so stale DNS cannot pin a dead address for the
        /// life of the singleton client.
        /// </summary>
        /// <returns>The handler; the caller owns it.</returns>
        internal static SocketsHttpHandler CreateTransportHandler()
        {
            return new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            };
        }

        /// <summary>
        /// Builds the HAR-verified search URL. A keyword-only search sends
        /// <c>urlType=search_by_keyword</c>; a located search sends
        /// <c>urlType=search_by_key_loc</c> and mirrors the query under the
        /// short <c>k</c>/<c>l</c> keys. <c>sort</c> is always sent; the
        /// optional filters are omitted when unset rather than sent empty,
        /// as the manual-search HAR does.
        /// </summary>
        /// <param name="criteria">The search criteria.</param>
        /// <param name="pageNumber">The one-based page number.</param>
        /// <returns>The absolute request URI.</returns>
        private static Uri SearchUri(SearchCriteria criteria, int pageNumber)
        {
            var locations = string.Join(", ", criteria.Locations);
            var located = criteria.Locations.Count > 0;
            var pairs = new List<string>(11)
            {
                Pair("noOfResults", PageSize.ToString(CultureInfo.InvariantCulture)),
                Pair("pageNo", pageNumber.ToString(CultureInfo.InvariantCulture)),
                Pair("urlType", located ? "search_by_key_loc" : "search_by_keyword"),
                Pair("searchType", "adv"),
                Pair("keyword", criteria.Keywords),
                Pair("location", locations),
            };

            // The located wire shape mirrors the query under the short keys;
            // a keyword-only search never carries them.
            if (located)
            {
                pairs.Add(Pair("k", criteria.Keywords));
                pairs.Add(Pair("l", locations));
            }

            pairs.Add(Pair("sort", "f"));
            if (criteria.ExperienceYears is { } experienceYears)
            {
                pairs.Add(
                    Pair("experience", experienceYears.ToString(CultureInfo.InvariantCulture))
                );
            }

            if (criteria.PostedWithinDays is { } postedWithinDays)
            {
                pairs.Add(Pair("jobAge", postedWithinDays.ToString(CultureInfo.InvariantCulture)));
            }

            return new Uri($"{SearchBaseUrl}?{string.Join('&', pairs)}", UriKind.Absolute);
        }

        /// <summary>
        /// Formats one query parameter, escaping the value.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <param name="value">The value, or null for an empty parameter.</param>
        /// <returns>The encoded pair.</returns>
        private static string Pair(string name, string? value)
        {
            return $"{name}={Uri.EscapeDataString(value ?? string.Empty)}";
        }

        /// <summary>
        /// Maps a <c>200</c> body. A body without a <c>jobDetails</c> (or
        /// fallback <c>jobs</c>) array is schema drift and becomes a wall; an
        /// empty array is a clean end.
        /// </summary>
        /// <param name="body">The response body.</param>
        /// <param name="pageNumber">The page number, for the warning.</param>
        /// <param name="uri">The request URI, for the drift warning.</param>
        /// <returns>The mapped page.</returns>
        private NaukriApiSearchPage ParsePage(string body, int pageNumber, Uri uri)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || RowArray(root) is not { } jobs)
                {
                    NaukriApiLog.Preview(_logger, uri, 200, "json", body);
                    return Wall(ChallengeKind.SourceChanged, 0.8, "json");
                }

                var rows = new List<NaukriApiRow>(jobs.GetArrayLength());
                var dropped = 0;
                foreach (var job in jobs.EnumerateArray())
                {
                    if (MapRow(job) is { } row)
                    {
                        rows.Add(row);
                    }
                    else
                    {
                        dropped++;
                    }
                }

                if (dropped > 0)
                {
                    LogRowsDropped(_logger, dropped, pageNumber);
                }

                var total = Integer(root, "noOfJobs") ?? 0;
                return new NaukriApiSearchPage(
                    rows,
                    Math.Max(total, rows.Count),
                    dropped,
                    rows.Count == 0,
                    ChallengeAssessment.Clear
                );
            }
            catch (JsonException)
            {
                NaukriApiLog.Preview(_logger, uri, 200, "json", body);
                return Wall(ChallengeKind.SourceChanged, 0.8, "json");
            }
        }

        /// <summary>
        /// Finds the search row array: the live body names it
        /// <c>jobDetails</c> (verified probe 2026-11-18), while <c>jobs</c>
        /// stays accepted for tolerance.
        /// </summary>
        /// <param name="root">The response object.</param>
        /// <returns>The array, or null when neither key holds one.</returns>
        private static JsonElement? RowArray(JsonElement root)
        {
            if (
                root.TryGetProperty("jobDetails", out var details)
                && details.ValueKind == JsonValueKind.Array
            )
            {
                return details;
            }

            if (root.TryGetProperty("jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array)
            {
                return jobs;
            }

            return null;
        }

        /// <summary>
        /// Maps one row through the same field helpers the HTML parser uses.
        /// A row without a usable title, job id, or Naukri job link is
        /// dropped.
        /// </summary>
        /// <param name="row">The raw row.</param>
        /// <returns>The mapped row, or null for a malformed one.</returns>
        private static NaukriApiRow? MapRow(JsonElement row)
        {
            if (row.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var title = NaukriFields.Clean(Text(row, "title"), JobTitle.MaxLength);
            if (title is null)
            {
                return null;
            }

            var rawUrl = Text(row, "jdURL");
            var hasUrl =
                NaukriFields.TryJobUrl(rawUrl, out var url, out var idFromUrl)
                || NaukriFields.TryJobUrl(Text(row, "staticUrl"), out url, out idFromUrl);
            if (!hasUrl)
            {
                return null;
            }

            var id = NaukriFields.NumericId(Text(row, "jobId")) ?? idFromUrl;
            if (id is null)
            {
                return null;
            }

            var experienceRaw = NaukriFields.Clean(Text(row, "experienceText"));
            var (minYears, maxYears) = NaukriFields.Experience(experienceRaw);
            if (experienceRaw is null)
            {
                var minimum = Integer(row, "minimumExperience");
                var maximum = Integer(row, "maximumExperience");
                if (minimum is >= 0 and <= 50 && maximum is >= 0 and <= 50 && minimum <= maximum)
                {
                    experienceRaw = FormattableString.Invariant($"{minimum}-{maximum} Yrs");
                    (minYears, maxYears) = NaukriFields.Experience(experienceRaw);
                }
            }

            var (salaryRaw, salary) = Salary(row);
            return new NaukriApiRow(
                new JobSnapshot
                {
                    Source = SourceKey.Naukri,
                    ExternalJobId = id,
                    Url = url,
                    Title = title,
                    Company = NaukriFields.Clean(Text(row, "companyName"), CompanyName.MaxLength),
                    Location = Location(row),
                    ExperienceRaw = experienceRaw,
                    ExperienceMinYears = minYears,
                    ExperienceMaxYears = maxYears,
                    SalaryRaw = salaryRaw,
                    Salary = salary,
                    PostedAt = CreatedAt(row),
                    Skills = NaukriFields.Skills(
                        (Text(row, "tagsAndSkills") ?? string.Empty).Split(',')
                    ),
                    ApplyUrl = NaukriFields.ExternalApplyUrl(Text(row, "clientCareersUrl"))
                        ?? NaukriFields.ExternalApplyUrl(Text(row, "applyRedirectUrl")),
                },
                IsExternalPortal(row)
            );
        }

        /// <summary>
        /// Maps the structured salary through the shared salary gate. Hidden
        /// salaries and non-INR currencies stay unset rather than guessed.
        /// </summary>
        /// <param name="row">The raw row.</param>
        /// <returns>The raw text and parsed range, both null when unusable.</returns>
        private static (string? Raw, SalaryRange? Value) Salary(JsonElement row)
        {
            if (
                !row.TryGetProperty("salaryDetail", out var detail)
                || detail.ValueKind != JsonValueKind.Object
                || Bool(detail, "hideSalary") == true
            )
            {
                return (null, null);
            }

            var minimum = Number(detail, "minimumSalary");
            var maximum = Number(detail, "maximumSalary");
            if (
                minimum is not { } min
                || maximum is not { } max
                || min <= 0
                || max < min
                || !string.Equals(
                    Text(detail, "currency")?.Trim(),
                    "INR",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return (null, null);
            }

            var raw = FormattableString.Invariant($"{min:0.##}-{max:0.##} PA");
            return NaukriFields.Salary(raw) is { } salary ? (raw, salary) : (null, null);
        }

        /// <summary>
        /// Reads the location placeholder's label and detects the work mode
        /// from it, as the HTML cards do.
        /// </summary>
        /// <param name="row">The raw row.</param>
        /// <returns>The location, or null when the row shows none.</returns>
        private static JobLocation? Location(JsonElement row)
        {
            if (
                !row.TryGetProperty("placeholders", out var placeholders)
                || placeholders.ValueKind != JsonValueKind.Array
            )
            {
                return null;
            }

            foreach (var placeholder in placeholders.EnumerateArray())
            {
                if (
                    placeholder.ValueKind == JsonValueKind.Object
                    && string.Equals(
                        Text(placeholder, "type"),
                        "location",
                        StringComparison.OrdinalIgnoreCase
                    )
                    && NaukriFields.Clean(Text(placeholder, "label")) is { } label
                )
                {
                    return NaukriFields.Location(label);
                }
            }

            return null;
        }

        /// <summary>
        /// Whether the row applies on a company portal.
        /// </summary>
        /// <param name="row">The raw row.</param>
        /// <returns>True for the three company-portal markers.</returns>
        private static bool IsExternalPortal(JsonElement row)
        {
            return Bool(row, "companyApplyJob") == true
                || !string.IsNullOrWhiteSpace(Text(row, "clientCareersUrl"))
                || !string.IsNullOrWhiteSpace(Text(row, "applyRedirectUrl"));
        }

        /// <summary>
        /// Reads the epoch-millisecond creation date.
        /// </summary>
        /// <param name="row">The raw row.</param>
        /// <returns>The UTC instant, or null when unusable.</returns>
        private static DateTimeOffset? CreatedAt(JsonElement row)
        {
            if (Number(row, "createdDate") is not { } value)
            {
                return null;
            }

            if (value is <= 0 or > 253_402_300_799_999)
            {
                return null;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds((long)value);
        }

        /// <summary>
        /// Reads a string or numeric property.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The text, or null when absent or of another kind.</returns>
        private static string? Text(JsonElement element, string name)
        {
            if (
                element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(name, out var value)
            )
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            };
        }

        /// <summary>
        /// Reads an integer property from a number or a numeric string.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The value, or null when absent or unparseable.</returns>
        private static int? Integer(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            return
                value.ValueKind == JsonValueKind.String
                && int.TryParse(
                    value.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out number
                )
                ? number
                : null;
        }

        /// <summary>
        /// Reads a decimal property from a number or a numeric string.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The value, or null when absent or unparseable.</returns>
        private static decimal? Number(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            {
                return number;
            }

            return
                value.ValueKind == JsonValueKind.String
                && decimal.TryParse(
                    value.GetString(),
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out number
                )
                ? number
                : null;
        }

        /// <summary>
        /// Reads a boolean property.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The value, or null when absent or of another kind.</returns>
        private static bool? Bool(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }

        /// <summary>
        /// Creates a blocked page carrying only its classification.
        /// </summary>
        /// <param name="kind">The wall kind.</param>
        /// <param name="confidence">Detector confidence.</param>
        /// <param name="evidence">Operator-safe evidence.</param>
        /// <returns>A page with no rows and no continuation.</returns>
        private static NaukriApiSearchPage Wall(
            ChallengeKind kind,
            double confidence,
            string evidence
        )
        {
            return new NaukriApiSearchPage(
                [],
                0,
                0,
                false,
                new ChallengeAssessment(kind, confidence, evidence)
            );
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Naukri API dropped {Count} malformed rows on page {Page}"
        )]
        private static partial void LogRowsDropped(ILogger logger, int count, int page);

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Naukri API request for page {Page} failed"
        )]
        private static partial void LogRequestFailed(ILogger logger, Exception exception, int page);
    }

    // CA1873 cannot see through these wrappers: every call below runs under
    // an IsEnabled guard, so the header and shape strings are built only
    // when the level is on. The analyzer only recognizes the direct call
    // shape.
#pragma warning disable CA1873
    /// <summary>
    /// The wire log shared by <see cref="NaukriApiClient"/> and
    /// <see cref="NaukriApiJobSource"/>: one information line per request
    /// with the full URI and its headers, one shape line per response, and a
    /// capped body preview on the failure path only.
    /// <para>
    /// Bodies never log in full at Information. The file sinks retain 90
    /// days, and the row and detail payloads carry every posting's full HTML
    /// description, so one failed page would flood the retention window with
    /// content nobody reads. The failure preview is the single exception and
    /// stops at <see cref="BodyPreviewLimit"/> characters.
    /// </para>
    /// </summary>
    internal static partial class NaukriApiLog
    {
        /// <summary>
        /// The most response-body characters a failure line may carry.
        /// </summary>
        internal const int BodyPreviewLimit = 2_000;

        // Enough to name a shape at a glance, small enough to stay one line.
        private const int KeyLimit = 20;

        // Only these header values log verbatim; no credential or cookie
        // ever travels under them. Every other header name keeps its value
        // hidden, nkparam included.
        private static readonly HashSet<string> PlaintextHeaders = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            "appid",
            "systemid",
            "Accept",
            "User-Agent",
        };

        /// <summary>
        /// Logs the request method, the full URI (query string included; no
        /// secret ever travels in the URL), and every header name with a
        /// value only for the plaintext allow-list.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="request">The built request.</param>
        internal static void Request(ILogger logger, HttpRequestMessage request)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogApiRequest(
                    logger,
                    request.Method.Method,
                    request.RequestUri?.AbsoluteUri ?? string.Empty,
                    Headers(request)
                );
            }
        }

        /// <summary>
        /// Logs a response's status and body shape (byte count plus the
        /// top-level JSON keys), never the body itself.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="uri">The request URI.</param>
        /// <param name="status">The response status.</param>
        /// <param name="body">The response body, summarized only.</param>
        internal static void Response(ILogger logger, Uri uri, int status, string body)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogApiResponse(logger, uri.AbsoluteUri, status, BodyShape(body));
            }
        }

        /// <summary>
        /// Logs a response outcome without its body: the clean listing end
        /// carries no wall, so nothing on it needs inspection.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="uri">The request URI.</param>
        /// <param name="status">The response status.</param>
        /// <param name="reason">The classification of the answer.</param>
        internal static void Outcome(ILogger logger, Uri uri, int status, string reason)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogApiResponse(logger, uri.AbsoluteUri, status, string.Concat("reason=", reason));
            }
        }

        /// <summary>
        /// Logs a wall: the status and reason at Information, then the body
        /// preview at Warning for the operator who has to inspect it.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="uri">The request URI.</param>
        /// <param name="status">The response status.</param>
        /// <param name="reason">The wall classification.</param>
        /// <param name="body">The response body; only its capped preview is logged.</param>
        internal static void Failure(
            ILogger logger,
            Uri uri,
            int status,
            string reason,
            string body
        )
        {
            Outcome(logger, uri, status, reason);
            Preview(logger, uri, status, reason, body);
        }

        /// <summary>
        /// Logs only the capped body preview at Warning. Used when the shape
        /// line is already written and the body is classified into a wall
        /// after parsing, such as schema drift on a <c>200</c>.
        /// </summary>
        /// <param name="logger">The logger to write to.</param>
        /// <param name="uri">The request URI.</param>
        /// <param name="status">The response status.</param>
        /// <param name="reason">The wall classification.</param>
        /// <param name="body">The response body; only its capped preview is logged.</param>
        internal static void Preview(
            ILogger logger,
            Uri uri,
            int status,
            string reason,
            string body
        )
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                var shown = Math.Min(body.Length, BodyPreviewLimit);
                LogApiFailure(
                    logger,
                    status,
                    uri.AbsoluteUri,
                    reason,
                    body.Length,
                    shown,
                    body[..shown]
                );
            }
        }

        /// <summary>
        /// Reads a rejected response's body for logging, degrading to an
        /// empty preview when the stream cannot be read: a classified wall
        /// must not become an exception just because its body is gone.
        /// Cancellation still propagates.
        /// </summary>
        /// <param name="response">The rejected response.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The body text, or an empty string when unreadable.</returns>
        internal static async Task<string> RejectedBodyAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken
        )
        {
            try
            {
                return await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (HttpRequestException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Summarizes a body as its UTF-8 byte count and at most
        /// <see cref="KeyLimit"/> top-level JSON keys.
        /// </summary>
        /// <param name="body">The response body.</param>
        /// <returns>The one-line shape.</returns>
        internal static string BodyShape(string body)
        {
            var bytes = Encoding.UTF8.GetByteCount(body);
            return string.Create(CultureInfo.InvariantCulture, $"bytes={bytes}; keys={Keys(body)}");
        }

        /// <summary>
        /// Renders the request headers as one line: names in full, values
        /// only for the allow-list, everything else as <c>[redacted]</c>.
        /// </summary>
        /// <param name="request">The built request.</param>
        /// <returns>The rendered header line.</returns>
        private static string Headers(HttpRequestMessage request)
        {
            List<string> rendered = [];
            foreach (var header in request.Headers)
            {
                // User-Agent is a space-separated field, not a
                // comma-combinable one: joining its parsed values with ", "
                // would misreport what went on the wire.
                var separator = string.Equals(
                    header.Key,
                    "User-Agent",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? " "
                    : ", ";
                var value = PlaintextHeaders.Contains(header.Key)
                    ? string.Join(separator, header.Value)
                    : "[redacted]";
                rendered.Add($"{header.Key}={value}");
            }

            return string.Join("; ", rendered);
        }

        /// <summary>
        /// Lists the top-level JSON keys of a body, bounded by
        /// <see cref="KeyLimit"/>.
        /// </summary>
        /// <param name="body">The response body.</param>
        /// <returns>The keys, or a shape marker when the body is not a JSON object.</returns>
        private static string Keys(string body)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return "<non-object>";
                }

                List<string> keys = [];
                var truncated = false;
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (keys.Count == KeyLimit)
                    {
                        truncated = true;
                        break;
                    }

                    keys.Add(property.Name);
                }

                var shape = string.Join(',', keys);
                return truncated ? shape + ",..." : shape;
            }
            catch (JsonException)
            {
                return "<invalid-json>";
            }
        }

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Naukri API {Method} {Uri} headers: {Headers}"
        )]
        private static partial void LogApiRequest(
            ILogger logger,
            string method,
            string uri,
            string headers
        );

        [LoggerMessage(
            Level = LogLevel.Information,
            Message = "Naukri API {Uri} answered {Status}: {Shape}"
        )]
        private static partial void LogApiResponse(
            ILogger logger,
            string uri,
            int status,
            string shape
        );

        // The one place a body may reach the log. It stops at the preview
        // limit because the sinks retain 90 days and the row/detail payloads
        // carry full HTML descriptions.
        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Naukri API {Status} wall on {Uri}: {Reason}; body ({TotalChars} chars, first {ShownChars} shown): {Body}"
        )]
        private static partial void LogApiFailure(
            ILogger logger,
            int status,
            string uri,
            string reason,
            int totalChars,
            int shownChars,
            string body
        );
    }
#pragma warning restore CA1873
}
