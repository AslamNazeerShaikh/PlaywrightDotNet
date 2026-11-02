using System.Text.Json;
using AngleSharp.Dom;

namespace JobScrape.Infrastructure.Sources.Indeed
{
    /// <summary>
    /// The fields the detail parser reads from a page's schema.org
    /// <c>JobPosting</c> JSON-LD block. Every value is raw text (or null when
    /// the block omits it); normalization happens in <see cref="IndeedFields"/>.
    /// </summary>
    internal sealed record IndeedJsonLd
    {
        /// <summary>
        /// Options tolerant of the hand-assembled JSON some pages ship.
        /// </summary>
        private static readonly JsonDocumentOptions Options = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };

        /// <summary>
        /// Gets the posting title.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Gets the description, as HTML.
        /// </summary>
        public string? DescriptionHtml { get; init; }

        /// <summary>
        /// Gets the ISO 8601 <c>datePosted</c>.
        /// </summary>
        public string? DatePosted { get; init; }

        /// <summary>
        /// Gets <c>hiringOrganization.name</c>.
        /// </summary>
        public string? Company { get; init; }

        /// <summary>
        /// Gets the <c>skills</c> entries (an array, or a comma/semicolon list split).
        /// </summary>
        public IReadOnlyList<string> Skills { get; init; } = [];

        /// <summary>
        /// Gets every <c>jobLocation.address.addressLocality</c> value.
        /// </summary>
        public IReadOnlyList<string> Localities { get; init; } = [];

        /// <summary>
        /// Gets <c>jobLocationType</c>, e.g. <c>TELECOMMUTE</c>.
        /// </summary>
        public string? LocationType { get; init; }

        /// <summary>
        /// Finds the first <c>JobPosting</c> block in the document. Blocks
        /// that are not valid JSON (even after replacing raw control
        /// characters) are skipped, never thrown.
        /// </summary>
        /// <param name="document">The page.</param>
        /// <returns>The posting fields, or null when the page has none.</returns>
        public static IndeedJsonLd? Find(IDocument document)
        {
            foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
            {
                using var json = TryParse(script.TextContent);
                if (json is not null && FindPosting(json.RootElement) is { } posting)
                {
                    return Read(posting);
                }
            }

            return null;
        }

        /// <summary>
        /// Parses JSON, retrying once with raw control characters (illegal
        /// inside JSON strings, but common in scraped descriptions) replaced.
        /// </summary>
        /// <param name="text">The script text.</param>
        /// <returns>The document, or null when it is not JSON.</returns>
        private static JsonDocument? TryParse(string text)
        {
            foreach (
                var candidate in new[]
                {
                    text,
                    new string(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray()),
                }
            )
            {
                try
                {
                    return JsonDocument.Parse(candidate, Options);
                }
                catch (JsonException)
                {
                    // Try the sanitized form, then give up on this block.
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a <c>JobPosting</c> object at the root, in a root array, or
        /// in an <c>@graph</c> array.
        /// </summary>
        /// <param name="root">The JSON root.</param>
        /// <returns>The posting object, or null.</returns>
        private static JsonElement? FindPosting(JsonElement root)
        {
            var candidates = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray().ToArray(),
                JsonValueKind.Object
                    when Property(root, "@graph") is { ValueKind: JsonValueKind.Array } graph =>
                [
                    root,
                    .. graph.EnumerateArray(),
                ],
                JsonValueKind.Object => [root],
                _ => [],
            };
            foreach (var candidate in candidates)
            {
                if (candidate.ValueKind == JsonValueKind.Object && IsJobPosting(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether an object's <c>@type</c> is (or includes) <c>JobPosting</c>.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>True for a job posting.</returns>
        private static bool IsJobPosting(JsonElement element)
        {
            return Strings(Property(element, "@type"))
                .Any(type => string.Equals(type, "JobPosting", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Copies the needed fields out of the posting object.
        /// </summary>
        /// <param name="posting">The posting object.</param>
        /// <returns>The fields.</returns>
        private static IndeedJsonLd Read(JsonElement posting)
        {
            var skills = Strings(Property(posting, "skills"))
                .SelectMany(skill => skill.Split([',', ';'], StringSplitOptions.TrimEntries))
                .ToArray();
            var locations = Property(posting, "jobLocation") is { } location
                ? location.ValueKind == JsonValueKind.Array
                    ? location.EnumerateArray().ToArray()
                    : [location]
                : [];

            return new IndeedJsonLd
            {
                Title = Scalar(Property(posting, "title")),
                DescriptionHtml = Scalar(Property(posting, "description")),
                DatePosted = Scalar(Property(posting, "datePosted")),
                Company = Scalar(Property(Property(posting, "hiringOrganization"), "name")),
                Skills = skills,
                Localities = locations
                    .SelectMany(place =>
                        Strings(Property(Property(place, "address"), "addressLocality"))
                    )
                    .ToArray(),
                LocationType = Strings(Property(posting, "jobLocationType")).FirstOrDefault(),
            };
        }

        /// <summary>
        /// A named property of an object, or null.
        /// </summary>
        /// <param name="element">The object, possibly null or not an object.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The property value, or null.</returns>
        private static JsonElement? Property(JsonElement? element, string name)
        {
            return
                element is { ValueKind: JsonValueKind.Object } value
                && value.TryGetProperty(name, out var property)
                ? property
                : null;
        }

        /// <summary>
        /// A string or number rendered as text, or null.
        /// </summary>
        /// <param name="element">The value.</param>
        /// <returns>The text, or null.</returns>
        private static string? Scalar(JsonElement? element)
        {
            return element?.ValueKind switch
            {
                JsonValueKind.String => element.Value.GetString(),
                JsonValueKind.Number => element.Value.GetRawText(),
                _ => null,
            };
        }

        /// <summary>
        /// The text values of a string, number, or array of them.
        /// </summary>
        /// <param name="element">The value.</param>
        /// <returns>The non-null texts.</returns>
        private static IEnumerable<string> Strings(JsonElement? element)
        {
            return element?.ValueKind == JsonValueKind.Array
                    ? element.Value.EnumerateArray().Select(item => Scalar(item)).OfType<string>()
                : Scalar(element) is { } single ? [single]
                : [];
        }
    }
}
