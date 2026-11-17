using System.Net;
using System.Text;
using JobScrape.Api.Http.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Xunit;

namespace JobScrape.Api.Tests
{
    /// <summary>
    /// The login limiter's partition key is derived before model binding, so
    /// it reads the request body itself. Reading must not leave synchronous
    /// IO enabled for the rest of the request.
    /// </summary>
    public sealed class AuthRegistrationTests
    {
        [Fact]
        public void PartitionKeyReadsTheEmailAndRestoresSynchronousIo()
        {
            var control = new RecordingBodyControl { AllowSynchronousIO = false };
            var context = new DefaultHttpContext();
            context.Features.Set<IHttpBodyControlFeature>(control);
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Body = new MemoryStream(
                Encoding.UTF8.GetBytes("""{"email":"user@example.com"}""")
            );

            var key = AuthRegistration.GetLoginPartitionKey(context);

            Assert.Equal("account:user@example.com", key);
            Assert.False(control.AllowSynchronousIO);
            // The body stays readable for the model binder.
            Assert.Equal(0, context.Request.Body.Position);
        }

        [Fact]
        public void PartitionKeyFallsBackToTheClientAddress()
        {
            var control = new RecordingBodyControl { AllowSynchronousIO = true };
            var context = new DefaultHttpContext();
            context.Features.Set<IHttpBodyControlFeature>(control);
            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));

            var key = AuthRegistration.GetLoginPartitionKey(context);

            Assert.Equal("ip:203.0.113.7", key);
            // The previous setting is restored even when it was already on.
            Assert.True(control.AllowSynchronousIO);
        }

        private sealed class RecordingBodyControl : IHttpBodyControlFeature
        {
            public bool AllowSynchronousIO { get; set; }
        }
    }
}
