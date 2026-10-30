using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ErrorTests
    {
        [Fact]
        public void Stores_all_fields()
        {
            var metadata = new Dictionary<string, object?> { ["runId"] = "abc" };

            var error = new Error("QUOTA_EXCEEDED", "Quota exceeded", "Daily cap reached.", ErrorKind.QuotaExceeded, metadata);

            Assert.Equal("QUOTA_EXCEEDED", error.Code);
            Assert.Equal("Quota exceeded", error.Title);
            Assert.Equal("Daily cap reached.", error.Detail);
            Assert.Equal(ErrorKind.QuotaExceeded, error.Kind);
            Assert.Same(metadata, error.Metadata);
        }

        [Fact]
        public void Metadata_defaults_to_null()
        {
            var error = new Error("INTERNAL_ERROR", "Title", "Detail", ErrorKind.Unexpected);

            Assert.Null(error.Metadata);
        }

        [Fact]
        public void Equal_records_are_equal()
        {
            var left = new Error("NOT_FOUND", "Title", "Detail", ErrorKind.NotFound);
            var right = new Error("NOT_FOUND", "Title", "Detail", ErrorKind.NotFound);

            Assert.Equal(left, right);
        }
    }
}
