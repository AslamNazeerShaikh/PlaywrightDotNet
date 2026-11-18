using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JobScrape.Infrastructure.Sources.Naukri;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Sources.Naukri
{
    public sealed class NaukriRequestTokenTests
    {
        [Fact]
        public void TokenDecodesToKeySize()
        {
            using var key = RSA.Create(2048);

            var token = NaukriRequestToken.Generate("srp", key);

            var cipher = Convert.FromBase64String(token);
            Assert.Equal(key.KeySize / 8, cipher.Length);
        }

        [Fact]
        public void TwoTokensForTheSamePageDiffer()
        {
            using var key = RSA.Create(2048);

            var first = NaukriRequestToken.Generate("srp", key);
            var second = NaukriRequestToken.Generate("srp", key);

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void AcceptsArbitraryPageTag()
        {
            using var key = RSA.Create(2048);

            foreach (var page in new[] { "srp", "4371234567" })
            {
                var token = NaukriRequestToken.Generate(page, key);
                var message = Encoding.UTF8.GetString(
                    key.Decrypt(Convert.FromBase64String(token), RSAEncryptionPadding.Pkcs1)
                );

                Assert.Matches(@"^v0\|\d{13}\|121_" + Regex.Escape(page) + "$", message);
            }
        }
    }
}
