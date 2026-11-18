using System.Security.Cryptography;
using System.Text;

namespace JobScrape.Infrastructure.Sources.Naukri
{
    /// <summary>
    /// Mints Naukri's per-request <c>nkparam</c> token: RSA PKCS#1 v1.5
    /// encryption of <c>v0|&lt;epoch-ms&gt;|121_&lt;page&gt;</c> under Naukri's
    /// published public key. Each call carries a fresh timestamp and fresh
    /// random padding, so a token is never replayed and no output is cached.
    /// The <c>page</c> tag is <c>srp</c> for search requests and the job id
    /// for detail requests (docs/superpowers/specs/2026-11-18-naukri-api-fast-path.md).
    /// </summary>
    public static class NaukriRequestToken
    {
        /// <summary>
        /// Naukri's published <c>nkparam</c> public key, base64-encoded
        /// SubjectPublicKeyInfo (512-bit RSA).
        /// Ported from JobSpy's <c>jobspy/naukri/constant.py</c>
        /// (https://github.com/speedyapply/JobSpy), MIT License.
        /// </summary>
        private const string PublicKeyBase64 =
            "MFwwDQYJKoZIhvcNAQEBBQADSwAwSAJBALrlQ+djR0RjJwBF1xuisHmdFv334MImK6LgzJhmLhN7"
            + "B5yuEyaKoasgXQk3+OQglsOaBxEJ0j5PcTL3nbOvt80CAwEAAQ==";

        /// <summary>
        /// Mints a token for <paramref name="page"/> under Naukri's published
        /// key. Call once per request; never cache or replay the result.
        /// </summary>
        /// <param name="page">The page tag: <c>srp</c> or a job id.</param>
        /// <returns>The base64 token for the <c>nkparam</c> header.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="page"/> is null, empty, or whitespace.</exception>
        public static string Generate(string page)
        {
            using var key = RSA.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(PublicKeyBase64), out _);
            return Generate(page, key);
        }

        /// <summary>
        /// Mints a token for <paramref name="page"/> under a supplied key. The
        /// key is not disposed here because the caller owns it.
        /// </summary>
        /// <param name="page">The page tag: <c>srp</c> or a job id.</param>
        /// <param name="key">The public key to encrypt under. Must not be null.</param>
        /// <returns>The base64 token for the <c>nkparam</c> header.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="page"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null.</exception>
        public static string Generate(string page, RSA key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(page);
            ArgumentNullException.ThrowIfNull(key);

            var message = $"v0|{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}|121_{page}";
            var cipher = key.Encrypt(Encoding.UTF8.GetBytes(message), RSAEncryptionPadding.Pkcs1);
            return Convert.ToBase64String(cipher);
        }
    }
}
