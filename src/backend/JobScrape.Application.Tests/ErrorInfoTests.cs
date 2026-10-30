using JobScrape.Application.Results;
using Xunit;

namespace JobScrape.Application.Tests
{
    public sealed class ErrorInfoTests
    {
        [Fact]
        public void StoresAllFields()
        {
            var metadata = new Dictionary<string, object?> { ["runId"] = "abc" };

            var error = new ErrorInfo(
                "QUOTA_EXCEEDED",
                "Quota exceeded",
                "Daily cap reached.",
                ErrorKind.QuotaExceeded,
                metadata
            );

            Assert.Equal("QUOTA_EXCEEDED", error.Code);
            Assert.Equal("Quota exceeded", error.Title);
            Assert.Equal("Daily cap reached.", error.Detail);
            Assert.Equal(ErrorKind.QuotaExceeded, error.Kind);
            Assert.Same(metadata, error.Metadata);
        }

        [Fact]
        public void MetadataDefaultsToNull()
        {
            var error = new ErrorInfo("INTERNAL_ERROR", "Title", "Detail", ErrorKind.Unexpected);

            Assert.Null(error.Metadata);
        }

        [Fact]
        public void EqualRecordsAreEqual()
        {
            var left = new ErrorInfo("NOT_FOUND", "Title", "Detail", ErrorKind.NotFound);
            var right = new ErrorInfo("NOT_FOUND", "Title", "Detail", ErrorKind.NotFound);

            Assert.Equal(left, right);
        }
    }
}
