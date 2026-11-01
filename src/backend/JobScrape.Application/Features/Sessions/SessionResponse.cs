using JobScrape.Domain.Access;

namespace JobScrape.Application.Features.Sessions
{
    /// <summary>
    /// Read-model view of a browser profile and its session health.
    /// Timestamps are UTC.
    /// </summary>
    /// <param name="Id">The browser profile identifier.</param>
    /// <param name="Name">The profile name (also its directory name).</param>
    /// <param name="SourceKey">The single source the profile is used for.</param>
    /// <param name="Engine"><c>Chromium</c>, <c>Firefox</c>, or <c>WebKit</c>.</param>
    /// <param name="Channel"><c>Bundled</c>, <c>Chrome</c>, or <c>Edge</c>.</param>
    /// <param name="State">Session state, e.g. <c>New</c>, <c>Healthy</c>, <c>CoolingDown</c>.</param>
    /// <param name="UserDataDirectory">Directory relative to the profiles root.</param>
    /// <param name="CanLaunch">Whether the profile may launch now.</param>
    /// <param name="CooldownUntil">When the cooldown ends, while cooling down.</param>
    /// <param name="ConsecutiveChallenges">Walls since the last successful verification.</param>
    /// <param name="LastUsedAt">When the profile was last launched for work.</param>
    /// <param name="LastVerifiedAt">When a probe last verified the session.</param>
    public sealed record SessionResponse(
        Guid Id,
        string Name,
        string SourceKey,
        string Engine,
        string Channel,
        string State,
        string UserDataDirectory,
        bool CanLaunch,
        DateTimeOffset? CooldownUntil,
        int ConsecutiveChallenges,
        DateTimeOffset? LastUsedAt,
        DateTimeOffset? LastVerifiedAt
    )
    {
        /// <summary>
        /// Projects a browser profile to its read model as of <paramref name="now"/>.
        /// </summary>
        /// <param name="profile">The profile. Must not be null.</param>
        /// <param name="now">The time used to decide whether it can launch.</param>
        /// <returns>The read model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="profile"/> is null.</exception>
        public static SessionResponse From(BrowserProfile profile, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return new SessionResponse(
                profile.Id.Value,
                profile.Name,
                profile.Source.Value,
                profile.Engine.ToString(),
                profile.Channel.ToString(),
                profile.State.ToString(),
                profile.UserDataDirectory,
                profile.CanLaunch(now),
                profile.CooldownUntil,
                profile.ConsecutiveChallenges,
                profile.LastUsedAt,
                profile.LastVerifiedAt
            );
        }
    }
}
