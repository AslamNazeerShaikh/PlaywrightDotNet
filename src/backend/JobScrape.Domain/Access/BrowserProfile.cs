using JobScrape.Domain.Abstractions;
using JobScrape.Domain.Common;

namespace JobScrape.Domain.Access
{
    /// <summary>
    /// Aggregate root for a persistent browser identity bound to one source:
    /// engine, channel, user-data directory, and session health
    /// (docs/03-domain-model.md §4.4, §6.2). Name uniqueness is enforced by
    /// the database.
    /// <para>
    /// The user-data directory is derived as <c>{source}/{name}</c> and the
    /// name is restricted to a safe path segment, so the directory can never
    /// escape the configured profiles root. A successful probe
    /// (<see cref="MarkVerified"/>) is the only way back to healthy.
    /// </para>
    /// </summary>
    public sealed class BrowserProfile : AggregateRoot
    {
        /// <summary>
        /// Consecutive walls after which the profile requires a reset.
        /// </summary>
        public const int MaxConsecutiveChallenges = 3;

        /// <summary>
        /// Maximum profile-name length.
        /// </summary>
        public const int MaxNameLength = 64;

        /// <summary>
        /// Initializes a new instance with already-validated state.
        /// </summary>
        /// <param name="id">The profile identifier.</param>
        /// <param name="name">The safe profile name.</param>
        /// <param name="source">The bound source.</param>
        /// <param name="engine">The browser engine.</param>
        /// <param name="channel">The engine channel.</param>
        private BrowserProfile(
            BrowserProfileId id,
            string name,
            SourceKey source,
            BrowserEngine engine,
            BrowserChannel channel
        )
        {
            Id = id;
            Name = name;
            Source = source;
            Engine = engine;
            Channel = channel;
            State = SessionState.New;
        }

        /// <summary>
        /// Gets the profile identifier.
        /// </summary>
        public BrowserProfileId Id { get; }

        /// <summary>
        /// Gets the name: letters, digits, <c>.</c>, <c>_</c>, <c>-</c>; it is
        /// also the directory name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the single source this profile is used for.
        /// </summary>
        public SourceKey Source { get; }

        /// <summary>
        /// Gets the browser engine.
        /// </summary>
        public BrowserEngine Engine { get; private set; }

        /// <summary>
        /// Gets the engine channel.
        /// </summary>
        public BrowserChannel Channel { get; private set; }

        /// <summary>
        /// Gets the user-data directory relative to the profiles root, with
        /// forward slashes, e.g. <c>naukri/naukri-main</c>.
        /// </summary>
        public string UserDataDirectory => $"{Source.Value}/{Name}";

        /// <summary>
        /// Gets the session health.
        /// </summary>
        public SessionState State { get; private set; }

        /// <summary>
        /// Gets when the current cooldown ends, while cooling down.
        /// </summary>
        public DateTimeOffset? CooldownUntil { get; private set; }

        /// <summary>
        /// Gets the walls hit since the last successful verification.
        /// </summary>
        public int ConsecutiveChallenges { get; private set; }

        /// <summary>
        /// Gets when the profile was last launched for work.
        /// </summary>
        public DateTimeOffset? LastUsedAt { get; private set; }

        /// <summary>
        /// Gets when a probe last verified the session.
        /// </summary>
        public DateTimeOffset? LastVerifiedAt { get; private set; }

        /// <summary>
        /// Creates a new, unverified profile.
        /// </summary>
        /// <param name="name">
        /// Profile name, trimmed; 1–64 characters of letters, digits,
        /// <c>.</c>, <c>_</c>, or <c>-</c>, not starting with <c>.</c>.
        /// </param>
        /// <param name="source">The source the profile is used for. Must not be null.</param>
        /// <param name="engine">The browser engine.</param>
        /// <param name="channel">The channel; branded channels require Chromium.</param>
        /// <returns>The new profile in <see cref="SessionState.New"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
        /// <exception cref="DomainException">Thrown when the name is unsafe or the engine/channel pair is invalid.</exception>
        public static BrowserProfile Create(
            string? name,
            SourceKey source,
            BrowserEngine engine,
            BrowserChannel channel
        )
        {
            ArgumentNullException.ThrowIfNull(source);

            var trimmed = EnsureValidName(name);
            EnsureValidEngineChannel(engine, channel);
            return new BrowserProfile(BrowserProfileId.New(), trimmed, source, engine, channel);
        }

        /// <summary>
        /// Renames the profile. The user-data directory keeps the old name
        /// until the caller moves it; this method changes metadata only.
        /// </summary>
        /// <param name="name">The new safe name.</param>
        /// <exception cref="DomainException">Thrown when the name is unsafe.</exception>
        public void Rename(string? name)
        {
            var trimmed = EnsureValidName(name);
            Name = trimmed;
        }

        /// <summary>
        /// Changes the browser engine and channel for the next launch. An
        /// open context keeps its current browser until it is released.
        /// </summary>
        /// <param name="engine">The new engine.</param>
        /// <param name="channel">The new channel; branded channels require Chromium.</param>
        /// <exception cref="DomainException">Thrown when the pair is invalid.</exception>
        public void Configure(BrowserEngine engine, BrowserChannel channel)
        {
            EnsureValidEngineChannel(engine, channel);
            Engine = engine;
            Channel = channel;
        }

        /// <summary>
        /// Trims and validates a profile name.
        /// </summary>
        /// <param name="name">The candidate name.</param>
        /// <returns>The trimmed name.</returns>
        /// <exception cref="DomainException">Thrown when the name is unsafe.</exception>
        private static string EnsureValidName(string? name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (!IsValidName(trimmed))
            {
                throw new DomainException(
                    $"Browser profile name must be 1..{MaxNameLength} of [A-Za-z0-9._-], not starting with '.'."
                );
            }

            return trimmed;
        }

        /// <summary>
        /// Validates an engine/channel pair.
        /// </summary>
        /// <param name="engine">The engine.</param>
        /// <param name="channel">The channel.</param>
        /// <exception cref="DomainException">Thrown when the pair is invalid.</exception>
        private static void EnsureValidEngineChannel(
            BrowserEngine engine,
            BrowserChannel channel
        )
        {
            if (!Enum.IsDefined(engine) || !Enum.IsDefined(channel))
            {
                throw new DomainException($"Unknown browser engine {engine} or channel {channel}.");
            }

            if (channel != BrowserChannel.Bundled && engine != BrowserEngine.Chromium)
            {
                throw new DomainException($"Channel {channel} requires the Chromium engine.");
            }
        }

        /// <summary>
        /// Whether <paramref name="name"/> is a safe profile name: 1–64
        /// characters of letters, digits, <c>.</c>, <c>_</c>, or <c>-</c>, not
        /// starting with <c>.</c>. The name is also a directory name, so this
        /// rule is what keeps the user-data directory inside the profiles root.
        /// Exposed so request validation applies the same rule.
        /// </summary>
        /// <param name="name">The trimmed candidate name.</param>
        /// <returns>True when the name is safe.</returns>
        public static bool IsValidName(string? name)
        {
            return name is { Length: > 0 and <= MaxNameLength }
                && name[0] != '.'
                && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
        }

        /// <summary>
        /// Whether the profile may be launched now: not when a reset is
        /// required, and not while a cooldown is running.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <returns>True when a launch is allowed.</returns>
        public bool CanLaunch(DateTimeOffset now)
        {
            return State switch
            {
                SessionState.ResetRequired => false,
                // CooldownUntil is always set while cooling down.
                SessionState.CoolingDown => now >= CooldownUntil.GetValueOrDefault(),
                _ => true,
            };
        }

        /// <summary>
        /// Records a launch for work.
        /// </summary>
        /// <param name="now">The launch time.</param>
        /// <exception cref="DomainException">Thrown when <see cref="CanLaunch"/> is false.</exception>
        public void MarkUsed(DateTimeOffset now)
        {
            EnsureCanLaunch(now);
            LastUsedAt = now;
        }

        /// <summary>
        /// Records a successful probe: the session is healthy and the
        /// challenge counter resets. The only way back to healthy after a wall.
        /// </summary>
        /// <param name="now">The verification time.</param>
        /// <exception cref="DomainException">Thrown when a reset is required or the cooldown has not ended.</exception>
        public void MarkVerified(DateTimeOffset now)
        {
            EnsureCanLaunch(now);
            State = SessionState.Healthy;
            ConsecutiveChallenges = 0;
            CooldownUntil = null;
            LastVerifiedAt = now;
            Raise(new SessionBootstrapped(Id, Source, State, now));
        }

        /// <summary>
        /// Records a wall: the profile cools down, or requires a reset after
        /// <see cref="MaxConsecutiveChallenges"/> consecutive walls.
        /// </summary>
        /// <param name="kind">
        /// The wall. <see cref="ChallengeKind.None"/>,
        /// <see cref="ChallengeKind.TransientError"/>, and
        /// <see cref="ChallengeKind.SourceChanged"/> are not session walls.
        /// </param>
        /// <param name="now">When the wall was hit.</param>
        /// <param name="cooldown">How long launches stay blocked; must be positive.</param>
        /// <exception cref="DomainException">
        /// Thrown when <paramref name="kind"/> is not a wall, the cooldown is
        /// not positive, or a reset is already required.
        /// </exception>
        public void MarkChallenged(ChallengeKind kind, DateTimeOffset now, TimeSpan cooldown)
        {
            if (
                kind
                    is ChallengeKind.None
                        or ChallengeKind.TransientError
                        or ChallengeKind.SourceChanged
                || !Enum.IsDefined(kind)
            )
            {
                throw new DomainException($"{kind} is not a session wall.");
            }

            if (cooldown <= TimeSpan.Zero)
            {
                throw new DomainException("A challenge cooldown must be positive.");
            }

            if (State == SessionState.ResetRequired)
            {
                throw new DomainException("The profile already requires a reset.");
            }

            ConsecutiveChallenges++;
            if (ConsecutiveChallenges >= MaxConsecutiveChallenges)
            {
                State = SessionState.ResetRequired;
                CooldownUntil = null;
                return;
            }

            State = SessionState.CoolingDown;
            CooldownUntil = now + cooldown;
        }

        /// <summary>
        /// Records that a healthy session's cookies expired; a re-login is needed.
        /// </summary>
        /// <exception cref="DomainException">Thrown when the profile is not healthy.</exception>
        public void MarkExpired()
        {
            if (State != SessionState.Healthy)
            {
                throw new DomainException($"Only a healthy session can expire (state {State}).");
            }

            State = SessionState.Expired;
        }

        /// <summary>
        /// Resets the profile to <see cref="SessionState.New"/>: counters,
        /// cooldown, and verification are cleared. The infrastructure layer
        /// wipes the user-data directory.
        /// </summary>
        public void Reset()
        {
            State = SessionState.New;
            ConsecutiveChallenges = 0;
            CooldownUntil = null;
            LastVerifiedAt = null;
        }

        /// <summary>
        /// Throws when <see cref="CanLaunch"/> is false.
        /// </summary>
        /// <param name="now">The current time.</param>
        /// <exception cref="DomainException">Thrown when a launch is not allowed.</exception>
        private void EnsureCanLaunch(DateTimeOffset now)
        {
            if (!CanLaunch(now))
            {
                throw new DomainException(
                    State == SessionState.ResetRequired
                        ? "The profile requires a reset."
                        : $"The profile is cooling down until {CooldownUntil:O}."
                );
            }
        }
    }
}
