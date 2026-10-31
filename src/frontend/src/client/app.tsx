import { useEffect, useMemo, useRef, useState } from "react";

import { Compass } from "./components/compass.tsx";
import { CodeTag } from "./components/modules.tsx";
import { RulerTabs } from "./components/rulertabs.tsx";
import { Scramble } from "./components/scramble.tsx";
import {
  type ApiClient,
  type ApiProblem,
  createApiClient,
  type HealthPayload,
  type SearchProfilePayload,
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
import { type LookupRecord, ProfilesPage } from "./pages/profiles.tsx";
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
  const [upstream, setUpstream] = useState("http://127.0.0.1:5207");
  const [bffLive, setBffLive] = useState(false);
  const [apiState, setApiState] = useState<"unknown" | "live" | "down">("unknown");

  const [health, setHealth] = useState<HealthPayload | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [checking, setChecking] = useState(false);
  const [samples, setSamples] = useState<Sample[]>([]);
  const [events, setEvents] = useState<LogEvent[]>([]);

  const [profile, setProfile] = useState<SearchProfilePayload | null>(null);
  const [profileProblem, setProfileProblem] = useState<ApiProblem | null>(null);
  const [looking, setLooking] = useState(false);
  const [history, setHistory] = useState<LookupRecord[]>([]);
  const probeBusy = useRef(false);
  const probeRef = useRef<() => void>(() => undefined);
  const linkLogged = useRef(false);
  // Default client must be identity-stable: a fresh object per render would
  // refire every [client] effect below and spiral into a fetch loop.
  const fallbackClient = useMemo(createApiClient, []);
  const activeClient = client ?? fallbackClient;

  const now = useNow();
  const heading =
    route === "overview" ? 0 : route === "health" ? 90 : route === "profiles" ? 180 : 270;
  const pageLabel = ROUTES.find((r) => r.id === route)?.label.toUpperCase() ?? "OVERVIEW";
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
    setChecking(true);
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
    setChecking(false);
    probeBusy.current = false;
  }

  probeRef.current = checkHealth;

  // Auto-probe: first hit lands on mount so the deck opens with data, then one
  // every 10s. The busy flag keeps a slow proxy from stacking probes; the
  // manual button rides the same path.
  useEffect(() => {
    probeRef.current();
    const timer = setInterval(() => {
      probeRef.current();
    }, 10_000);
    return () => clearInterval(timer);
  }, []);

  async function lookupProfile(id: string) {
    setLooking(true);
    const { result, ms, at } = await activeClient.fetchProfile(id);
    const code = result.ok ? "OK" : result.problem.code;
    if (result.ok) {
      setProfile(result.value);
      setProfileProblem(null);
      setApiState("live");
    } else {
      setProfile(null);
      setProfileProblem(result.problem);
      setApiState(planeDown(result.problem) ? "down" : "live");
    }
    const outcome = (result.ok ? "ok" : "err") as LookupRecord["outcome"];
    setHistory((h) => [{ id, at: clockTime(at), outcome, code }, ...h].slice(0, 20));
    setEvents((e) =>
      pushEvent(e, {
        at: clockTime(at),
        kind: result.ok ? "ok" : "err",
        code,
        detail: result.ok
          ? `${result.value.name} · ${result.value.sourceKey}`
          : result.problem.title,
      }),
    );
    setLooking(false);
  }

  return (
    <div className="deck-root" data-density={density} data-theme={theme}>
      <div className="aurora" aria-hidden="true">
        <div className="aurora-blob blob-1" />
        <div className="aurora-blob blob-2" />
        <div className="aurora-blob blob-3" />
      </div>
      <div className="scanbeam" aria-hidden="true" />
      <header className="cmdbar">
        <div className="brand">
          <span className="brand-mark">
            <Scramble text="JOBSCRAPE//OPS-DECK" duration={1350} />
          </span>
          <span className="brand-sub">LOCAL CONTROL PLANE</span>
        </div>
        <div className="cmdbar-meta">
          <span className="clock" aria-label="Current UTC time">
            {now} UTC
          </span>
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

      <Compass heading={heading} page={pageLabel} />

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
            profile={profile}
            problem={profileProblem}
            looking={looking}
            history={history}
            onLookup={lookupProfile}
          />
        )}
        {route === "topology" && <TopologyPage upstream={upstream} />}
      </main>

      <footer style={{ padding: "0 18px 24px", color: "var(--faint)", fontSize: "11px" }}>
        <CodeTag code="STABLE-CODES" /> UI branches on codes, never prose · Bun + React, no Node ·
        IBM Plex Mono
      </footer>
    </div>
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
