using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Configuration;
using JobScrape.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace JobScrape.Infrastructure.Tests.Security
{
    public sealed class JwtTokenServiceTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 6, 8, 0, 0, TimeSpan.Zero);
        private const string Secret = "test-secret-that-is-at-least-32-chars!!";

        private static JwtOptions TestOptions()
        {
            return new JwtOptions
            {
                Secret = Secret,
                Issuer = "jobscrape-test",
                Audience = "jobscrape-test-api",
                TokenLifetimeMinutes = 60,
                MaxFailedAttempts = 5,
                LockoutMinutes = 15,
            };
        }

        private static (User User, Role Role) ActiveAdmin()
        {
            var user = User.Create("op@example.com", PasswordHash.Create("Seed-Secret1!!"), Now);
            user.CompleteFirstLogin(PasswordHash.Create("Final-Password2@@"), Now);
            var role = Role.Create(RoleId.New(), RoleName.Admin);
            role.Grant(PermissionKey.From("runs.read"));
            user.AssignRole(role.Id);
            return (user, role);
        }

        private static JwtSecurityToken Read(string token)
        {
            return new JwtSecurityTokenHandler().ReadJwtToken(token);
        }

        [Fact]
        public void IssuesExpectedClaimsAndExpiry()
        {
            var service = new JwtTokenService(Options.Create(TestOptions()));
            var (user, role) = ActiveAdmin();

            var token = service.CreateToken(user, [role], [.. role.PermissionKeys], Now);
            var jwt = Read(token);

            Assert.Equal(user.Id.Value.ToString(), jwt.Subject);
            Assert.Equal("op@example.com", jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
            Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
            Assert.Contains(jwt.Claims, c => c.Type == "perm" && c.Value == "runs.read");
            Assert.False(string.IsNullOrWhiteSpace(jwt.Id));
            Assert.DoesNotContain(jwt.Claims, c => c.Type == "must_change_password");
            Assert.Equal(Now.AddMinutes(60), jwt.ValidTo);
            Assert.Equal(new DateTime(2026, 11, 6, 8, 0, 0, DateTimeKind.Utc), jwt.ValidFrom);
        }

        [Fact]
        public void FlagsPendingFirstLogin()
        {
            var service = new JwtTokenService(Options.Create(TestOptions()));
            var user = User.Create("op@example.com", PasswordHash.Create("Seed-Secret1!!"), Now);

            var token = service.CreateToken(user, [], [], Now);

            Assert.Contains(Read(token).Claims, c => c.Type == "must_change_password" && c.Value == "true");
        }

        [Fact]
        public void ValidatesAgainstTheConfiguredSecretAndRejectsOthers()
        {
            var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(TestOptions()));
            var (user, role) = ActiveAdmin();
            var issuedAt = DateTimeOffset.UtcNow;
            var token = service.CreateToken(user, [role], [.. role.PermissionKeys], issuedAt);

            TokenValidationParameters Parameters(string secret)
            {
                return new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "jobscrape-test",
                    ValidateAudience = true,
                    ValidAudience = "jobscrape-test-api",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                };
            }

            var principal = new JwtSecurityTokenHandler().ValidateToken(
                token,
                Parameters(Secret),
                out _
            );
            Assert.Equal(user.Id.Value.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            Assert.ThrowsAny<SecurityTokenException>(() =>
                new JwtSecurityTokenHandler().ValidateToken(
                    token,
                    Parameters("a-different-secret-that-is-also-long-enough!!"),
                    out _
                )
            );
        }

        [Fact]
        public void RejectsNullOptions()
        {
            Assert.Throws<ArgumentNullException>(() => new JwtTokenService(null!));
        }
    }
}
