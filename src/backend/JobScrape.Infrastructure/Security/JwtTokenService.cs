using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JobScrape.Application.Abstractions;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JobScrape.Infrastructure.Security
{
    /// <summary>
    /// Issues HS256 bearer tokens. Claims carry the user id (<c>sub</c>),
    /// email, role names, one <c>perm</c> claim per granted permission, a
    /// token id (<c>jti</c>), the token version (<c>ver</c>), and
    /// <c>must_change_password</c> while first login is pending. Expiry comes
    /// from <see cref="JwtOptions"/>.
    /// </summary>
    public sealed class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _options;
        private readonly JwtSecurityTokenHandler _handler = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtTokenService"/> class.
        /// </summary>
        /// <param name="options">The validated JWT options. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
        public JwtTokenService(IOptions<JwtOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            _options = options.Value;
        }

        /// <inheritdoc />
        public string CreateToken(
            User user,
            IReadOnlyList<Role> roles,
            IReadOnlyList<PermissionKey> permissions,
            DateTimeOffset now
        )
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(roles);
            ArgumentNullException.ThrowIfNull(permissions);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.Value.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
                new(
                    IdentityClaimTypes.TokenVersion,
                    user.TokenVersion.ToString(CultureInfo.InvariantCulture)
                ),
            };
            foreach (var role in roles)
            {
                claims.Add(new Claim(IdentityClaimTypes.Role, role.Name.ToString()));
            }

            foreach (var permission in permissions)
            {
                claims.Add(new Claim(IdentityClaimTypes.Permission, permission.Value));
            }

            if (user.MustChangePassword)
            {
                claims.Add(new Claim(IdentityClaimTypes.MustChangePassword, "true"));
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                NotBefore = now.UtcDateTime,
                Expires = now.AddMinutes(_options.TokenLifetimeMinutes).UtcDateTime,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            };
            return _handler.WriteToken(_handler.CreateToken(descriptor));
        }
    }
}
