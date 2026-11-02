import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { CodeTag } from "./components/modules.tsx";
import { RulerTabs } from "./components/rulertabs.tsx";
import { Scramble } from "./components/scramble.tsx";
import {
  type ApiClient,
  type ApiProblem,
  createApiClient,
  type CreateProfileInput,
  type CreateSessionInput,
  type HealthPayload,
  type SearchProfilePayload,
  type SessionPayload,
  type SourceList,
} from "./lib/api.ts";
import { useRenderLog } from "./lib/debug.ts";
import { type DeckRoute, parseRoute, ROUTES } from "./lib/router.ts";
import {
  latencyStats,
  type LogEvent,
  pushEvent,
  pushSample,
  type Sample,
} from "./lib/telemetry.ts";
import { HealthPage } from "./pages/health.tsx";
import { OverviewPage } from "./pages/overview.tsx";
import { ProfilesPage } from "./pages/profiles.tsx";
import { RunsPage } from "./pages/runs.tsx";
import { SessionsPage } from "./pages/sessions.tsx";
import { SourcesPage } from "./pages/sources.tsx";
import { TopologyPage } from "./pages/topology.tsx";

/**
 * Ops-deck shell: command bar (brand, UTC clock, upstream pill, density switch), tab rail with one
 * tab per feature, and the routed page. Telemetry (samples, event log, lookup history) lives here
 * so every page reads the same flight recorder.
 */
export function App({ client, initialRoute }: { client?: ApiClient; initialRoute?: DeckRoute }) {
  useRenderLog("App");
  const [route, setRoute] = useState<DeckRoute>(initialRoute ?? readHash());
  const [density, setDensity] = useState<"compact" | "cozy">("cozy");
  const [theme, setTheme] = useState<"dark" | "light">("light");
  const [scanRed, setScanRed] = useState(false);
  const [upstream, setUpstream] = useState("https://localhost:5208");
  const [bffLive, setBffLive] = useState(false);
  const [apiState, setApiState] = useState<"unknown" | "live" | "down">("unknown");

  const [health, setHealth] = useState<HealthPayload | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [checking, setChecking] = useState(false);
  const [samples, setSamples] = useState<Sample[]>([]);
  const [events, setEvents] = useState<LogEvent[]>([]);

  const [profiles, setProfiles] = useState<SearchProfilePayload[] | null>(null);
  const [profilesProblem, setProfilesProblem] = useState<ApiProblem | null>(null);
  const [profilesLoading, setProfilesLoading] = useState(false);
  const [creating, setCreating] = useState(false);
  const [createProblem, setCreateProblem] = useState<ApiProblem | null>(null);
  const [selectedProfileId, setSelectedProfileId] = useState<string | null>(null);
  const [sources, setSources] = useState<SourceList | null>(null);
  const [sourcesProblem, setSourcesProblem] = useState<ApiProblem | null>(null);
  const [sourcesLoading, setSourcesLoading] = useState(false);
  const [sessions, setSessions] = useState<SessionPayload[] | null>(null);
  const [sessionsProblem, setSessionsProblem] = useState<ApiProblem | null>(null);
  const [sessionsLoading, setSessionsLoading] = useState(false);
  const [creatingSession, setCreatingSession] = useState(false);
  const [createSessionProblem, setCreateSessionProblem] = useState<ApiProblem | null>(null);
  const probeBusy = useRef(false);
  const lastProbeAt = useRef(0);
  const probeRef = useRef<() => void>(() => undefined);
  // Latest-wins tickets per list: a response may only write state while its
  // request is still the newest one for that list.
  const tickets = useRef({ profiles: 0, sources: 0, sessions: 0 });
  const linkLogged = useRef(false);
  // Default client must be identity-stable: a fresh object per render would
  // refire every [client] effect below and spiral into a fetch loop.
  const fallbackClient = useMemo(createApiClient, []);
  const activeClient = client ?? fallbackClient;
  // Stable sink for pages that log to the killfeed from their own effects.
  const logEvent = useCallback((event: LogEvent) => setEvents((e) => pushEvent(e, event)), []);

  const stats = latencyStats(samples);

  // Theme lives on <html> so body-level surfaces (page grid texture,
  // command bar) follow the deck theme without extra plumbing.
  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    return () => {
      delete document.documentElement.dataset.theme;
    };
  }, [theme]);

  // Hash is the route: back/forward buttons and deep links work with zero config.
  useEffect(() => {
    if (initialRoute !== undefined) {
      return;
    }
    const onHash = () => setRoute(parseRoute(window.location.hash));
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, [initialRoute]);

  // The BFF reports its own upstream on /healthz — the deck displays the
  // wire it actually runs on, not a build-time guess. The link event fires
  // once: without the guard, its state update would refire this effect and
  // spiral into a fetch loop.
  useEffect(() => {
    let cancelled = false;
    activeClient
      .fetchMeta()
      .then(({ result }) => {
        if (cancelled || !result.ok) {
          return;
        }
        setBffLive(true);
        setUpstream(result.value.upstream);
        if (!linkLogged.current) {
          linkLogged.current = true;
          setEvents((e) =>
            pushEvent(e, {
              at: clockTime(new Date().toISOString()),
              kind: "info",
              code: "BFF_LINK",
              detail: `BFF live · upstream ${result.value.upstream}`,
            }),
          );
        }
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [activeClient]);

  async function checkHealth() {
    if (probeBusy.current) {
      return;
    }
    probeBusy.current = true;
    lastProbeAt.current = Date.now();
    setChecking(true);
    // finally: a throw must not leave the busy flag set, or every later
    // probe (auto and manual) would return early forever.
    try {
      const { result, ms, at } = await activeClient.fetchHealth();
      const ok = result.ok;
      setSamples((s) => pushSample(s, { label: "health", ms, at: Date.parse(at), ok }));
      if (ok) {
        setHealth(result.value);
        setProblem(null);
        setApiState("live");
      } else {
        setHealth(null);
        setProblem(result.problem);
        // A 502/0 proves the plane is down; any other answer proves it lives.
        setApiState(planeDown(result.problem) ? "down" : "live");
      }
      setEvents((e) =>
        pushEvent(e, {
          at: clockTime(at),
          kind: ok ? "ok" : "err",
          code: ok ? "HEALTHY" : result.problem.code,
          detail: ok
            ? `engine healthy in ${ms < 10 ? ms.toFixed(1) : Math.round(ms)} ms`
            : result.problem.title,
        }),
      );
    } finally {
      setChecking(false);
      probeBusy.current = false;
    }
  }

  probeRef.current = checkHealth;

  // Auto-probe: first hit lands on mount so the deck opens with data, then one
  // per interval while the deck is visible. A hidden tab skips its ticks and
  // probes once on return if the last probe is stale, so a background deck
  // costs the API nothing. The busy flag keeps a slow proxy from stacking
  // probes; the manual button rides the same path.
  useEffect(() => {
    const poll = () => {
      if (shouldProbe(document.visibilityState, lastProbeAt.current, Date.now())) {
        probeRef.current();
      }
    };
    probeRef.current();
    const timer = setInterval(poll, PROBE_INTERVAL_MS);
    document.addEventListener("visibilitychange", poll);
    return () => {
      clearInterval(timer);
      document.removeEventListener("visibilitychange", poll);
    };
  }, []);

  async function loadProfiles() {
    const ticket = ++tickets.current.profiles;
    setProfilesLoading(true);
    const { result, at } = await activeClient.listProfiles();
    if (ticket !== tickets.current.profiles) {
      return;
    }
    if (result.ok) {
      setProfiles(result.value.items);
      setProfilesProblem(null);
      setApiState("live");
    } else {
      setProfilesProblem(result.problem);
      setApiState(planeDown(result.problem) ? "down" : "live");
      setEvents((e) =>
        pushEvent(e, {
          at: clockTime(at),
          kind: "err",
          code: result.problem.code,
          detail: result.problem.title,
        }),
      );
    }
    setProfilesLoading(false);
  }

  async function createProfile(input: CreateProfileInput): Promise<boolean> {
    setCreating(true);
    const { result, at } = await activeClient.createProfile(input);
    setCreating(false);
    if (!result.ok) {
      setCreateProblem(result.problem);
      setApiState(planeDown(result.problem) ? "down" : "live");
      setEvents((e) =>
        pushEvent(e, {
          at: clockTime(at),
          kind: "err",
          code: result.problem.code,
          detail: result.problem.title,
        }),
      );
      return false;
    }
    setCreateProblem(null);
    setSelectedProfileId(result.value.id);
    setEvents((e) =>
      pushEvent(e, {
        at: clockTime(at),
        kind: "ok",
        code: "CREATED",
        detail: `${result.value.name} · ${result.value.sourceKey}`,
      }),
    );
    await loadProfiles();
    return true;
  }

  async function loadSources() {
    const ticket = ++tickets.current.sources;
    setSourcesLoading(true);
    const { result } = await activeClient.listSources();
    if (ticket !== tickets.current.sources) {
      return;
    }
    if (result.ok) {
      setSources(result.value);
      setSourcesProblem(null);
      setApiState("live");
    } else {
      setSourcesProblem(result.problem);
      setApiState(planeDown(result.problem) ? "down" : "live");
    }
    setSourcesLoading(false);
  }

  async function loadSessions() {
    const ticket = ++tickets.current.sessions;
    setSessionsLoading(true);
    const { result } = await activeClient.listSessions();
    if (ticket !== tickets.current.sessions) {
      return;
    }
    if (result.ok) {
      setSessions(result.value.items);
      setSessionsProblem(null);
      setApiState("live");
    } else {
      setSessionsProblem(result.problem);
      setApiState(planeDown(result.problem) ? "down" : "live");
    }
    setSessionsLoading(false);
  }

  async function createSession(input: CreateSessionInput): Promise<boolean> {
    setCreatingSession(true);
    const { result, at } = await activeClient.createSession(input);
    setCreatingSession(false);
    setEvents((e) =>
      pushEvent(e, {
        at: clockTime(at),
        kind: result.ok ? "ok" : "err",
        code: result.ok ? "CREATED" : result.problem.code,
        detail: result.ok
          ? `${result.value.name} · ${result.value.sourceKey}`
          : result.problem.title,
      }),
    );
    if (!result.ok) {
      setCreateSessionProblem(result.problem);
      setApiState(planeDown(result.problem) ? "down" : "live");
      return false;
    }
    setCreateSessionProblem(null);
    await loadSessions();
    return true;
  }

  const loadersRef = useRef({ loadProfiles, loadSources, loadSessions });
  loadersRef.current = { loadProfiles, loadSources, loadSessions };

  // Page data loads whenever its page opens; the ref keeps the effect keyed
  // on the route alone, so renders never refetch.
  useEffect(() => {
    if (route === "profiles") {
      void loadersRef.current.loadProfiles();
    }
    if (route === "sources") {
      void loadersRef.current.loadSources();
    }
    if (route === "sessions") {
      void loadersRef.current.loadSessions();
    }
  }, [route]);

  useEffect(() => {
    if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
      return;
    }
    let cancelled = false;
    let glowTimer: ReturnType<typeof setTimeout> | null = null;
    let offTimer: ReturnType<typeof setTimeout> | null = null;
    const schedule = () => {
      const delay = 3000 + Math.random() * 6000;
      glowTimer = setTimeout(() => {
        if (cancelled) return;
        setScanRed(true);
        offTimer = setTimeout(() => {
          if (cancelled) return;
          setScanRed(false);
          schedule();
        }, 900 + Math.random() * 700);
      }, delay);
    };
    schedule();
    return () => {
      cancelled = true;
      if (glowTimer !== null) clearTimeout(glowTimer);
      if (offTimer !== null) clearTimeout(offTimer);
    };
  }, []);

  return (
    <div className="deck-root" data-density={density} data-theme={theme}>
      <div className="aurora" aria-hidden="true">
        <div className="aurora-blob blob-1" />
        <div className="aurora-blob blob-2" />
        <div className="aurora-blob blob-3" />
      </div>
      <div className={`scanbeam${scanRed ? " scanbeam-red" : ""}`} aria-hidden="true" />
      <header className="cmdbar">
        <div className="brand">
          <span className="brand-mark">
            <Scramble text="JOBSCRAPE//OPS-DECK" duration={1350} />
          </span>
          <span className="brand-sub">LOCAL CONTROL PLANE</span>
        </div>
        <div className="cmdbar-meta">
          <UtcClock />
          <span className="pill" title="BFF self-check">
            <span className={`dot${bffLive ? " live" : ""}`} /> BFF {bffLive ? "LIVE" : "…"}
          </span>
          <span className="pill" title="Upstream data-plane URL reported by the BFF">
            <span
              className={`dot${apiState === "live" ? " live" : apiState === "down" ? " bad" : ""}`}
            />{" "}
            API {apiState.toUpperCase()} · {upstream}
          </span>
          <button
            type="button"
            className="density-btn"
            onClick={() => setTheme((t) => (t === "dark" ? "light" : "dark"))}
            title="Toggle color theme"
          >
            THEME: {theme.toUpperCase()}
          </button>
          <button
            type="button"
            className="density-btn"
            onClick={() => setDensity((d) => (d === "compact" ? "cozy" : "compact"))}
            title="Toggle module density"
          >
            DENSITY: {density.toUpperCase()}
          </button>
        </div>
      </header>


      <RulerTabs
        route={route}
        onSelect={
          initialRoute === undefined
            ? (next) => {
                setRoute(next);
              }
            : undefined
        }
      />

      <main className="deck" key={route}>
        {route === "overview" && (
          <OverviewPage
            health={health}
            healthMs={stats?.last ?? null}
            stats={stats}
            samples={samples}
            events={events}
            apiState={apiState}
          />
        )}
        {route === "health" && (
          <HealthPage
            health={health}
            problem={problem}
            checking={checking}
            samples={samples}
            stats={stats}
            onCheck={checkHealth}
          />
        )}
        {route === "profiles" && (
          <ProfilesPage
            profiles={profiles}
            listProblem={profilesProblem}
            loading={profilesLoading}
            creating={creating}
            createProblem={createProblem}
            selectedId={selectedProfileId}
            onSelect={setSelectedProfileId}
            onCreate={createProfile}
            onRefresh={() => void loadProfiles()}
          />
        )}
        {route === "sources" && (
          <SourcesPage
            sources={sources}
            problem={sourcesProblem}
            loading={sourcesLoading}
            onRefresh={() => void loadSources()}
          />
        )}
        {route === "sessions" && (
          <SessionsPage
            sessions={sessions}
            listProblem={sessionsProblem}
            loading={sessionsLoading}
            creating={creatingSession}
            createProblem={createSessionProblem}
            onCreate={createSession}
            onRefresh={() => void loadSessions()}
          />
        )}
        {route === "runs" && <RunsPage client={activeClient} onEvent={logEvent} />}
        {route === "topology" && <TopologyPage upstream={upstream} />}
      </main>

      <footer style={{ padding: "0 18px 24px", color: "var(--faint)", fontSize: "11px" }}>
        <CodeTag code="STABLE-CODES" /> UI branches on codes, never prose · Bun + React, no Node ·
        IBM Plex Mono
      </footer>
    </div>
  );
}

/** Health auto-probe cadence. */
export const PROBE_INTERVAL_MS = 10_000;

/**
 * Whether the auto-probe should hit the API now: only for a visible deck, and only when the last
 * probe is about one interval old (a tab flipping back and forth must not fire a probe each time).
 * One second of slack keeps timer jitter from skipping a regular tick.
 */
export function shouldProbe(
  visibility: DocumentVisibilityState,
  lastProbeAt: number,
  now: number,
): boolean {
  return visibility === "visible" && now - lastProbeAt >= PROBE_INTERVAL_MS - 1000;
}

/**
 * UTC clock. It owns its one-second tick so only this span re-renders: held in App, the tick
 * re-rendered the whole deck every second (the profiler showed 25-54 components per commit).
 */
function UtcClock() {
  useRenderLog("UtcClock");
  const now = useNow();
  return (
    <span className="clock" aria-label="Current UTC time">
      {now} UTC
    </span>
  );
}

/** Ticks once a second; unmount clears the interval. */
function useNow(): string {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(timer);
  }, []);
  return clockTime(now.toISOString());
}

function readHash(): DeckRoute {
  if (typeof window === "undefined" || typeof window.location === "undefined") {
    return "overview";
  }
  return parseRoute(window.location.hash);
}

function clockTime(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}:${pad(d.getUTCSeconds())}`;
}

/** 502/0 means the wire or the plane is down; any other answer proves it lives. */
function planeDown(problem: ApiProblem): boolean {
  return problem.code === "DOTNET_UNREACHABLE" || problem.code === "BFF_UNREACHABLE";
}
