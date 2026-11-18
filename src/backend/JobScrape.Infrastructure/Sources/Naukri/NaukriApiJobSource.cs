using System.Globalization;
using System.Text.Json;
using JobScrape.Application.Features.Scraping;
using JobScrape.Domain.Access;
using JobScrape.Domain.Common;
using JobScrape.Domain.Sourcing;
using JobScrape.Infrastructure.Sources;
using Microsoft.Extensions.Logging;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// The API fast path for Naukri (docs/superpowers/specs/2026-11-18-naukri-api-fast-path.md):
    /// serves the same <see cref="IJobSource"/> contract as
    /// <see cref="NaukriJobSource"/> but fetches listing pages from
    /// <c>jobapi/v3/search</c> and details from <c>jobapi/v4/job/{id}</c>.
    /// No browser object is touched. Pacing, caps, quota, retries, and
    /// pagination windows stay with <see cref="RunScrapeHandler"/>; this
    /// adapter maps one request into the handler's page contract.
    /// <para>
    /// Walls that a human would solve in a browser (interstitials, CAPTCHA,
    /// login redirects, access-denied pages) are reported as
    /// <see cref="ChallengeKind.SourceChanged"/>. The policy bans login and
    /// cookie handling, so no human action could clear them; the run ends
    /// with its partial results instead of parking in a manual pause.
    /// </para>
    /// </summary>
    public sealed partial class NaukriApiJobSource : IJobSource, IDisposable
    {
        /// <summary>
        /// The opaque cursor meaning "request the next API page".
        /// </summary>
        public const string NextCursor = "api-next";

        private const string DetailBaseUrl = "https://www.naukri.com/jobapi/v4/job/";

        // Three 406s in a row is the rotated-key signature, not a transient
        // throttle: the same request shape fails for everyone until the
        // embedded public key is replaced.
        private const int KeyRotationThreshold = 3;

        private const string UserAgent =
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/142.0.0.0 Safari/537.36";

        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(25);

        // All injected and never mutated.
        private readonly NaukriApiClient _client;
        private readonly HttpClient _detailClient;
        private readonly ILogger<NaukriApiJobSource> _logger;

        // Consecutive 406 answers to listing requests. The embedded key is
        // process-wide, so the count is too: one run's 406 followed by the
        // next run's 406 is the same broken-key symptom.
        private int _consecutive406;

        /// <summary>
        /// Initializes a new instance with the production detail transport:
        /// redirects disabled, a 25-second timeout, and two-minute connection
        /// rotation.
        /// </summary>
        /// <param name="client">The search client. Must not be null.</param>
        /// <param name="logger">The logger. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public NaukriApiJobSource(NaukriApiClient client, ILogger<NaukriApiJobSource> logger)
            : this(client, CreateDetailClient(), logger) { }

        /// <summary>
        /// Initializes a new instance over a supplied detail client. The
        /// adapter owns and disposes it; callers must not reuse it.
        /// </summary>
        /// <param name="client">The search client. Must not be null.</param>
        /// <param name="detailClient">The detail HTTP client. Must not be null.</param>
        /// <param name="logger">The logger. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        internal NaukriApiJobSource(
            NaukriApiClient client,
            HttpClient detailClient,
            ILogger<NaukriApiJobSource> logger
        )
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(detailClient);
            ArgumentNullException.ThrowIfNull(logger);
            _client = client;
            _detailClient = detailClient;
            _logger = logger;
        }

        /// <inheritdoc />
        public SourceKey Key => SourceKey.Naukri;

        /// <inheritdoc />
        public SourceCapabilities Capabilities { get; } = new(SupportsDetails: true);

        /// <summary>
        /// Fetches one <c>jobapi/v3/search</c> page and maps it onto the
        /// handler's page contract. External-portal rows are skipped and
        /// duplicate ids on the page collapse to the first one.
        /// </summary>
        /// <param name="context">The run being served. Must not be null.</param>
        /// <param name="request">Which page to fetch. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The mapped page, or the wall that stopped the listing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="StartPageOutOfRangeException">
        /// Thrown when the requested start page is past the listing's last page.
        /// </exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<SearchPageResult> SearchPageAsync(
            SourceRunContext context,
            SearchPageRequest request,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(request);

            var page = await _client.FetchPageAsync(
                context.Criteria,
                request.PageNumber,
                cancellationToken
            );
            if (page.Challenge.Kind == ChallengeKind.RateLimited)
            {
                return SearchPageResult.Blocked(RateLimitedWall(page.Challenge));
            }

            Interlocked.Exchange(ref _consecutive406, 0);
            if (page.Challenge.Kind != ChallengeKind.None)
            {
                return SearchPageResult.Blocked(FetchWall(page.Challenge));
            }

            var lastPage = LastPage(page.Total);
            if (request.PageNumber == 1 && lastPage is { } last && context.StartPage > last)
            {
                throw new StartPageOutOfRangeException(context.StartPage, last);
            }

            var jobs = PageRows(page.Rows, context.RunId.Value, request.PageNumber);
            // The reported total is authoritative: a page can lose malformed
            // rows and still have a next page. The short-page check is only
            // the fallback when the response carried no total.
            var hasMore =
                !page.EndOfListing
                && page.Rows.Count > 0
                && (
                    page.Total > 0
                        ? request.PageNumber * NaukriApiClient.PageSize < page.Total
                        : page.Rows.Count == NaukriApiClient.PageSize
                );
            return new SearchPageResult(
                jobs,
                hasMore ? NextCursor : null,
                jobs.Count == 0
                    ? null
                    : new PageFingerprint(
                        jobs[0].ExternalJobId,
                        Range(request.PageNumber, page.Rows.Count, page.Total)
                    ),
                ChallengeAssessment.Clear,
                lastPage
            );
        }

        /// <summary>
        /// Fetches one posting's detail from <c>jobapi/v4/job/{id}</c> with a
        /// fresh <c>nkparam</c> minted under the job id.
        /// </summary>
        /// <param name="context">The run being served. Must not be null.</param>
        /// <param name="job">The posting to enrich. Must not be null.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The detail snapshot, or the classified failure.</returns>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<JobDetailResult> GetDetailAsync(
            SourceRunContext context,
            JobReference job,
            CancellationToken cancellationToken
        )
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(job);

            var uri = new Uri(
                DetailBaseUrl + Uri.EscapeDataString(job.ExternalJobId.Value),
                UriKind.Absolute
            );
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            var token = NaukriRequestToken.Generate(job.ExternalJobId.Value);
            request.Headers.TryAddWithoutValidation("appid", "121");
            request.Headers.TryAddWithoutValidation("systemid", "Naukri");
            request.Headers.TryAddWithoutValidation("nkparam", token);
            request.Headers.Accept.Add(new("application/json"));
            request.Headers.UserAgent.ParseAdd(UserAgent);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                NaukriApiLog.Request(
                    _logger,
                    request.Method.Method,
                    request.RequestUri?.AbsoluteUri ?? string.Empty,
                    NaukriApiLog.Headers(request)
                );
            }

            HttpResponseMessage response;
            try
            {
                response = await _detailClient.SendAsync(
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
                LogDetailFailed(_logger, ex, job.ExternalJobId.Value);
                return new JobDetailResult(
                    null,
                    new ChallengeAssessment(ChallengeKind.TransientError, 0.8, "network")
                );
            }
            catch (OperationCanceledException ex)
            {
                LogDetailFailed(_logger, ex, job.ExternalJobId.Value);
                return new JobDetailResult(
                    null,
                    new ChallengeAssessment(ChallengeKind.TransientError, 0.8, "timeout")
                );
            }

            using (response)
            {
                switch ((int)response.StatusCode)
                {
                    case 200:
                    {
                        var body = await response.Content.ReadAsStringAsync(cancellationToken);
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            NaukriApiLog.Response(_logger, uri, 200, NaukriApiLog.BodyShape(body));
                        }

                        return ParseDetail(body, job, uri, token);
                    }
                    case 406:
                        return await RejectedAsync(
                            response,
                            uri,
                            406,
                            "rate-limited",
                            token,
                            new JobDetailResult(
                                null,
                                new ChallengeAssessment(
                                    ChallengeKind.RateLimited,
                                    0.95,
                                    "status=406"
                                )
                            ),
                            cancellationToken
                        );
                    case >= 300 and < 400:
                        return await RejectedAsync(
                            response,
                            uri,
                            (int)response.StatusCode,
                            "redirect",
                            token,
                            new JobDetailResult(
                                null,
                                FetchWall(
                                    new ChallengeAssessment(
                                        ChallengeKind.SoftChallenge,
                                        0.9,
                                        string.Create(
                                            CultureInfo.InvariantCulture,
                                            $"status={(int)response.StatusCode}"
                                        )
                                    )
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
                            token,
                            new JobDetailResult(
                                null,
                                new ChallengeAssessment(
                                    ChallengeKind.TransientError,
                                    0.8,
                                    string.Create(
                                        CultureInfo.InvariantCulture,
                                        $"status={(int)response.StatusCode}"
                                    )
                                )
                            ),
                            cancellationToken
                        );
                }
            }
        }

        /// <summary>
        /// Reads and logs a rejected detail response's body, then returns the
        /// already-classified result. Only the failure path may log body
        /// content, and only through the shared capped, redacted preview.
        /// </summary>
        /// <param name="response">The rejected response.</param>
        /// <param name="uri">The request URI, for the log line.</param>
        /// <param name="status">The response status.</param>
        /// <param name="reason">The classification, for the log line.</param>
        /// <param name="token">The request's token, stripped from the preview.</param>
        /// <param name="result">The pre-classified detail result.</param>
        /// <param name="cancellationToken">Token to observe for cancellation.</param>
        /// <returns>The detail result.</returns>
        private async Task<JobDetailResult> RejectedAsync(
            HttpResponseMessage response,
            Uri uri,
            int status,
            string reason,
            string token,
            JobDetailResult result,
            CancellationToken cancellationToken
        )
        {
            var body = await NaukriApiLog.RejectedBodyAsync(response, cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                NaukriApiLog.Outcome(_logger, uri, status, string.Concat("reason=", reason));
            }

            if (_logger.IsEnabled(LogLevel.Warning))
            {
                NaukriApiLog.Preview(_logger, uri, status, reason, body, token);
            }

            return result;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _detailClient.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Maps a wall onto the API path. Anything a human would solve in a
        /// browser becomes <see cref="ChallengeKind.SourceChanged"/>: the
        /// run fails with its partial results, and the original kind stays
        /// in the evidence for the operator.
        /// </summary>
        /// <param name="assessment">The classified response.</param>
        /// <returns>The wall the handler should act on.</returns>
        private static ChallengeAssessment FetchWall(ChallengeAssessment assessment)
        {
            if (
                assessment.Kind
                is ChallengeKind.SourceChanged
                    or ChallengeKind.TransientError
                    or ChallengeKind.RateLimited
            )
            {
                return assessment;
            }

            var evidence = string.IsNullOrWhiteSpace(assessment.Evidence)
                ? assessment.Kind.ToString()
                : $"{assessment.Kind}: {assessment.Evidence}";
            return new ChallengeAssessment(
                ChallengeKind.SourceChanged,
                assessment.Confidence,
                evidence
            );
        }

        /// <summary>
        /// Names key rotation once consecutive 406 responses prove the key
        /// itself is stale; a plain rate limit keeps the client's evidence.
        /// </summary>
        /// <param name="assessment">The client's 406 classification.</param>
        /// <returns>The rate-limit wall for the handler.</returns>
        private ChallengeAssessment RateLimitedWall(ChallengeAssessment assessment)
        {
            var count = Interlocked.Increment(ref _consecutive406);
            if (count < KeyRotationThreshold)
            {
                return assessment;
            }

            LogKeyRotationSuspected(_logger, count);
            return new ChallengeAssessment(
                ChallengeKind.RateLimited,
                assessment.Confidence,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{assessment.Evidence}; {count} consecutive 406s: key rotation likely"
                )
            );
        }

        /// <summary>
        /// Keeps the rows a run may collect: company-portal postings are
        /// skipped (never silently collected as ordinary postings), and a
        /// duplicated id on the page keeps its first row.
        /// </summary>
        /// <param name="rows">The page's mapped rows.</param>
        /// <param name="runId">The run, for the warning.</param>
        /// <param name="pageNumber">The page number, for the warning.</param>
        /// <returns>The collectable snapshots in page order.</returns>
        private List<JobSnapshot> PageRows(
            IReadOnlyList<NaukriApiRow> rows,
            Guid runId,
            int pageNumber
        )
        {
            var jobs = new List<JobSnapshot>(rows.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var external = 0;
            foreach (var row in rows)
            {
                if (row.ExternalPortal)
                {
                    external++;
                    continue;
                }

                if (seen.Add(row.Snapshot.ExternalJobId))
                {
                    jobs.Add(row.Snapshot);
                }
            }

            if (external > 0)
            {
                LogExternalSkipped(_logger, runId, pageNumber, external);
            }

            return jobs;
        }

        /// <summary>
        /// The listing's last page for the 20-row page size, or null when
        /// the page reported no total.
        /// </summary>
        /// <param name="total">The reported listing size.</param>
        /// <returns>The last page, or null.</returns>
        private static int? LastPage(int total)
        {
            return total <= 0
                ? null
                : Math.Max(1, (total + NaukriApiClient.PageSize - 1) / NaukriApiClient.PageSize);
        }

        /// <summary>
        /// Formats the page's observed result range for the fingerprint.
        /// </summary>
        /// <param name="pageNumber">The one-based page number.</param>
        /// <param name="rowCount">How many rows the page carried.</param>
        /// <param name="total">The reported listing size, or 0 when absent.</param>
        /// <returns>The range text.</returns>
        private static string Range(int pageNumber, int rowCount, int total)
        {
            var first = ((pageNumber - 1) * NaukriApiClient.PageSize) + 1;
            var last = first + rowCount - 1;
            return total > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{first} - {last} of {total}")
                : string.Create(CultureInfo.InvariantCulture, $"{first} - {last}");
        }

        /// <summary>
        /// Maps a <c>200</c> detail body. A body without an object is schema
        /// drift; a usable object without a title is an unusable detail.
        /// </summary>
        /// <param name="body">The response body.</param>
        /// <param name="job">The posting being enriched.</param>
        /// <param name="uri">The request URI, for the drift warning.</param>
        /// <param name="token">The request's token, stripped from the drift preview.</param>
        /// <returns>The detail snapshot, or the classification.</returns>
        private JobDetailResult ParseDetail(string body, JobReference job, Uri uri, string token)
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        NaukriApiLog.Preview(_logger, uri, 200, "json", body, token);
                    }

                    return new JobDetailResult(
                        null,
                        new ChallengeAssessment(ChallengeKind.SourceChanged, 0.8, "json")
                    );
                }

                var details = root.TryGetProperty("jobDetails", out var nested) ? nested : root;
                if (details.ValueKind != JsonValueKind.Object)
                {
                    if (_logger.IsEnabled(LogLevel.Warning))
                    {
                        NaukriApiLog.Preview(_logger, uri, 200, "json", body, token);
                    }

                    return new JobDetailResult(
                        null,
                        new ChallengeAssessment(ChallengeKind.SourceChanged, 0.8, "json")
                    );
                }

                var title = NaukriFields.Clean(Text(details, "title"), JobTitle.MaxLength);
                if (title is null)
                {
                    return new JobDetailResult(null, ChallengeAssessment.Clear);
                }

                var id = NaukriFields.NumericId(Text(details, "jobId")) ?? job.ExternalJobId.Value;
                var url = job.Url.Value;
                if (
                    NaukriFields.TryJobUrl(
                        Text(details, "jdURL") ?? Text(details, "staticUrl"),
                        out var canonical,
                        out _
                    )
                )
                {
                    url = canonical;
                }

                var companyDetail =
                    details.TryGetProperty("companyDetail", out var company)
                    && company.ValueKind == JsonValueKind.Object
                        ? company
                        : default;
                var experienceRaw = NaukriFields.Clean(Text(details, "experienceText"));
                var (minYears, maxYears) = NaukriFields.Experience(experienceRaw);
                if (experienceRaw is null)
                {
                    var minimum = Integer(details, "minimumExperience");
                    var maximum = Integer(details, "maximumExperience");
                    if (
                        minimum is >= 0 and <= 50
                        && maximum is >= 0 and <= 50
                        && minimum <= maximum
                    )
                    {
                        experienceRaw = FormattableString.Invariant($"{minimum}-{maximum} Yrs");
                        (minYears, maxYears) = NaukriFields.Experience(experienceRaw);
                    }
                }

                var (salaryRaw, salary) = Salary(details);
                return new JobDetailResult(
                    new JobSnapshot
                    {
                        Source = SourceKey.Naukri,
                        ExternalJobId = id,
                        Url = url,
                        Title = title,
                        Company = NaukriFields.Clean(
                            Text(details, "companyName")
                                ?? Text(companyDetail, "name")
                                ?? Text(companyDetail, "label"),
                            CompanyName.MaxLength
                        ),
                        CompanyUrl = NaukriFields.CompanyPageUrl(
                            Text(companyDetail, "companyUrl") ?? Text(details, "companyUrl")
                        ),
                        Location = Location(details),
                        ExperienceRaw = experienceRaw,
                        ExperienceMinYears = minYears,
                        ExperienceMaxYears = maxYears,
                        SalaryRaw = salaryRaw,
                        Salary = salary,
                        PostedAt = CreatedAt(details)
                            ?? NaukriFields.IsoDate(
                                Text(details, "postedOn") ?? Text(details, "postedDate")
                            ),
                        Description = Description(details),
                        Skills = Skills(details),
                        ApplyUrl = NaukriFields.ExternalApplyUrl(Text(details, "clientCareersUrl"))
                            ?? NaukriFields.ExternalApplyUrl(Text(details, "applyRedirectUrl")),
                        LowConfidence = false,
                    },
                    ChallengeAssessment.Clear
                );
            }
            catch (JsonException)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    NaukriApiLog.Preview(_logger, uri, 200, "json", body, token);
                }

                return new JobDetailResult(
                    null,
                    new ChallengeAssessment(ChallengeKind.SourceChanged, 0.8, "json")
                );
            }
        }

        /// <summary>
        /// Converts the detail's HTML description to capped plain text.
        /// </summary>
        /// <param name="details">The detail object.</param>
        /// <returns>The plain text, or null when the response has none.</returns>
        private static string? Description(JsonElement details)
        {
            var html = Text(details, "description")
                ?? Text(details, "jobDescription")
                ?? Text(details, "shortDescription");
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            using var fragment = NaukriHtml.Parse(html);
            return fragment.Body is { } body
                ? NaukriFields.LongText(
                    HtmlPlainText.ToPlainText(body, static line => NaukriFields.Clean(line))
                )
                : null;
        }

        /// <summary>
        /// Reads the skill tags as a comma string or an array of labels.
        /// </summary>
        /// <param name="details">The detail object.</param>
        /// <returns>The cleaned skills.</returns>
        private static IReadOnlyList<string> Skills(JsonElement details)
        {
            if (!details.TryGetProperty("tagsAndSkills", out var skills))
            {
                return [];
            }

            if (skills.ValueKind == JsonValueKind.String)
            {
                return NaukriFields.Skills((skills.GetString() ?? string.Empty).Split(','));
            }

            return skills.ValueKind == JsonValueKind.Array
                ? NaukriFields.Skills(
                    skills
                        .EnumerateArray()
                        .Select(skill =>
                            skill.ValueKind == JsonValueKind.Object ? Text(skill, "label")
                            : skill.ValueKind == JsonValueKind.String ? skill.GetString()
                            : null
                        )
                )
                : [];
        }

        /// <summary>
        /// Maps the structured detail salary. Hidden salaries and non-INR
        /// currencies stay unset rather than guessed.
        /// </summary>
        /// <param name="details">The detail object.</param>
        /// <returns>The raw text and parsed range, both null when unusable.</returns>
        private static (string? Raw, SalaryRange? Value) Salary(JsonElement details)
        {
            if (
                !details.TryGetProperty("salaryDetail", out var detail)
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
        /// from it, as the search rows do.
        /// </summary>
        /// <param name="details">The detail object.</param>
        /// <returns>The location, or null when the response shows none.</returns>
        private static JobLocation? Location(JsonElement details)
        {
            if (
                !details.TryGetProperty("placeholders", out var placeholders)
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
        /// Reads the epoch-millisecond creation date.
        /// </summary>
        /// <param name="details">The detail object.</param>
        /// <returns>The UTC instant, or null when unusable.</returns>
        private static DateTimeOffset? CreatedAt(JsonElement details)
        {
            if (Number(details, "createdDate") is not { } value)
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
        /// Creates the production detail transport with the same posture as
        /// the search client.
        /// </summary>
        /// <returns>The HTTP client; the adapter owns it.</returns>
        private static HttpClient CreateDetailClient()
        {
            return new HttpClient(NaukriApiClient.CreateTransportHandler())
            {
                Timeout = RequestTimeout,
            };
        }

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Naukri API run {RunId} page {Page} skipped {Count} company-portal rows"
        )]
        private static partial void LogExternalSkipped(
            ILogger logger,
            Guid runId,
            int page,
            int count
        );

        [LoggerMessage(
            Level = LogLevel.Warning,
            Message = "Naukri API answered {Count} consecutive 406s; the embedded API key has likely rotated"
        )]
        private static partial void LogKeyRotationSuspected(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Naukri API detail {JobId} failed")]
        private static partial void LogDetailFailed(
            ILogger logger,
            Exception exception,
            string jobId
        );
    }
}
