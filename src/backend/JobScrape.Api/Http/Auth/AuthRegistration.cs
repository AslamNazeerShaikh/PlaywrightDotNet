using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using JobScrape.Api.Http;
using JobScrape.Application.Abstractions;
using JobScrape.Application.Results;
using JobScrape.Domain;
using JobScrape.Domain.Identity;
using JobScrape.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace JobScrape.Api.Http.Auth
{
    /// <summary>
    /// The rate-limit policy names the API edge enforces.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>
        /// The per-account login partition.
        /// </summary>
        public const string Login = "login";

        /// <summary>
        /// The separate setup-password partition.
        /// </summary>
        public const string Setup = "setup";
    }

    /// <summary>
    /// Wires HS256 bearer authentication, the admin and per-permission
    /// authorization policies, and the fixed-window rate limiters. JWT
    /// settings come from the validated <c>JobScrape:Jwt</c> section; the
    /// secret is never logged.
    /// </summary>
    public static class AuthRegistration
    {
        /// <summary>
        /// The global request ceiling, per client IP.
        /// </summary>
        internal const int GlobalPermitLimit = 100;

        /// <summary>
        /// The stricter auth-surface ceiling, per client IP and per account.
        /// Shared by the per-account policy limiters and the IP-scoped
        /// middleware limiter.
        /// </summary>
        internal const int LoginPermitLimit = 10;

        /// <summary>
        /// Registers bearer authentication with the fallback policy requiring
        /// an authenticated user on every endpoint that does not opt out.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddJobScrapeAuth(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
            services
                .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>(
                    (options, jwtOptions) =>
                    {
                        var jwt = jwtOptions.Value;
                        // Keep the JWT claim names as issued (sub, email, perm):
                        // the default inbound mapping would rename them and the
                        // edge reads the issued names everywhere.
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = jwt.Issuer,
                            ValidateAudience = true,
                            ValidAudience = jwt.Audience,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(jwt.Secret)
                            ),
                            ClockSkew = TimeSpan.Zero,
                            RoleClaimType = IdentityClaimTypes.Role,
                        };
                        options.Events = new JwtBearerEvents
                        {
                            OnChallenge = context =>
                            {
                                context.HandleResponse();
                                return Errors
                                    .Unauthenticated()
                                    .ToProblem()
                                    .ExecuteAsync(context.HttpContext);
                            },
                            OnForbidden = context =>
                                Errors.AccessDenied().ToProblem().ExecuteAsync(context.HttpContext),
                            OnTokenValidated = async context =>
                            {
                                var users =
                                    context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                                var sub = context
                                    .Principal?.FindFirst(IdentityClaimTypes.Subject)
                                    ?.Value;
                                var version = context
                                    .Principal?.FindFirst(IdentityClaimTypes.TokenVersion)
                                    ?.Value;
                                if (!Guid.TryParse(sub, out var userId) || version is null)
                                {
                                    context.Fail("The token is missing its identity.");
                                    return;
                                }

                                User? user;
                                try
                                {
                                    user = await users.GetAsync(
                                        UserId.From(userId),
                                        context.HttpContext.RequestAborted
                                    );
                                }
                                catch (DomainException)
                                {
                                    context.Fail("The token subject is invalid.");
                                    return;
                                }

                                if (
                                    user is null
                                    || !user
                                        .TokenVersion.ToString(CultureInfo.InvariantCulture)
                                        .Equals(version, StringComparison.Ordinal)
                                )
                                {
                                    context.Fail(
                                        "The token was revoked by a password change or lockout."
                                    );
                                }
                            },
                        };
                    }
                );

            var authorization = services.AddAuthorizationBuilder();
            authorization.SetFallbackPolicy(
                new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
            );
            authorization.AddPolicy(
                AuthPolicies.RequireAdmin,
                policy => policy.AddRequirements(new AdminRequirement())
            );
            foreach (var permission in PermissionCatalog.Keys)
            {
                authorization.AddPolicy(
                    permission.Value,
                    policy => policy.AddRequirements(new PermissionRequirement(permission.Value))
                );
            }

            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();
            return services;
        }

        /// <summary>
        /// Registers the fixed-window limiters: 100 requests per minute per
        /// IP globally, 10 per minute per account on login and setup, and 10
        /// per minute per IP on login and setup via the middleware limiter.
        /// Rejections render as 429 Problem Details with a stable code, never
        /// an empty body.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The same service collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
        public static IServiceCollection AddJobScrapeRateLimiting(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                    new ValueTask(WriteRateLimitedProblemAsync(context.HttpContext));
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: context.Connection.RemoteIpAddress?.ToString()
                                ?? "unknown",
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = GlobalPermitLimit,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0,
                                AutoReplenishment = true,
                            }
                        )
                );
                // Per-account login budget, keyed by the submitted email
                // when present, otherwise the client IP.
                options.AddPolicy(
                    RateLimitPolicies.Login,
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetLoginPartitionKey(context),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = LoginPermitLimit,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0,
                                AutoReplenishment = true,
                            }
                        )
                );

                // Separate setup-password budget, so setup spam cannot burn an
                // account's login budget.
                options.AddPolicy(
                    RateLimitPolicies.Setup,
                    context =>
                        RateLimitPartition.GetFixedWindowLimiter(
                            partitionKey: GetLoginPartitionKey(context),
                            factory: _ => new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = LoginPermitLimit,
                                Window = TimeSpan.FromMinutes(1),
                                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                                QueueLimit = 0,
                                AutoReplenishment = true,
                            }
                        )
                );
            });
            return services;
        }

        /// <summary>
        /// Builds the login-limiter partition key: the account identifier
        /// (email) parsed from the request body when present, otherwise the
        /// client IP.
        /// </summary>
        private static string GetLoginPartitionKey(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            context.Request.EnableBuffering();
            var bodyControl = context.Features.Get<IHttpBodyControlFeature>();
            if (bodyControl is not null)
            {
                bodyControl.AllowSynchronousIO = true;
            }

            string? email = null;
            if (context.Request.Body.CanSeek)
            {
                context.Request.Body.Position = 0;
                using var reader = new StreamReader(
                    context.Request.Body,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024,
                    leaveOpen: true
                );
                var body = reader.ReadToEnd();
                context.Request.Body.Position = 0;
                if (!string.IsNullOrWhiteSpace(body))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(body);
                        if (
                            document.RootElement.ValueKind == JsonValueKind.Object
                            && document.RootElement.TryGetProperty("email", out var property)
                            && property.ValueKind == JsonValueKind.String
                        )
                        {
                            email = property.GetString();
                        }
                    }
                    catch (JsonException)
                    {
                        // Not JSON or no email field: fall back to IP.
                    }
                }
            }

            return string.IsNullOrWhiteSpace(email)
                ? $"ip:{context.Connection.RemoteIpAddress}"
                : $"account:{email}";
        }

        /// <summary>
        /// Renders the shared 429 rejection body used by both the registered
        /// limiters and the IP-scoped middleware limiter, so every rejection
        /// has an identical shape.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <returns>A task that completes when the response is written.</returns>
        internal static Task WriteRateLimitedProblemAsync(HttpContext context)
        {
            context.Response.Headers.RetryAfter = "60";
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return Errors.TooManyRequests().ToProblem().ExecuteAsync(context);
        }
    }
}
