/**
 * Typed same-origin API helpers. Every call goes to the BFF (`/api/*`), which proxies loopback-only
 * to the .NET data plane. Calls are timed with `performance.now()` so the deck can chart proxy
 * latency without a second measurement path. Failures surface as {@link ApiProblem} carrying the
 * stable `code` contract — the UI maps codes, never prose (see docs/14-application-patterns.md
 * §6).
 */
export interface HealthPayload {
  status: string;
  timeUtc: string;
}

/** A saved search profile as the API returns it (delays in ms, timestamps ISO UTC). */
export interface SearchProfilePayload {
  id: string;
  name: string;
  sourceKey: string;
  keywords: string;
  locations: string[];
  experienceYears: number | null;
  postedWithinDays: number | null;
  remoteOnly: boolean;
  requiredSkills: string[];
  maxPages: number;
  maxJobs: number;
  maxDetails: number;
  minDelayMs: number;
  maxDelayMs: number;
  enabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SearchProfileList {
  items: SearchProfilePayload[];
  total: number;
}

/** One configured source: policy, compliance gate, today's quota, and circuit state. */
export interface SourceStatus {
  key: string;
  tier: "Standard" | "Restricted" | "HighRisk";
  enabled: boolean;
  gate: { allowed: boolean; reason: string | null };
  policy: {
    maxPages: number;
    maxJobs: number;
    maxDetails: number;
    minDelayMs: number;
    maxDelayMs: number;
    challengeCooldownMinutes: number;
  };
  quota: {
    date: string;
    pagesUsed: number;
    pageCap: number;
    detailsUsed: number;
    detailCap: number;
    runsUsed: number;
    runCap: number;
    challenges: number;
  };
  circuit: {
    state: "Closed" | "Open" | "HalfOpen";
    openUntil: string | null;
    canProbe: boolean;
    consecutiveFailures: number;
    recentFailures: number;
  };
}

export interface SourceList {
  items: SourceStatus[];
  asOf: string;
}

/** A browser profile and its session health (timestamps ISO UTC). */
export interface SessionPayload {
  id: string;
  name: string;
  sourceKey: string;
  engine: "Chromium" | "Firefox" | "WebKit";
  channel: "Bundled" | "Chrome" | "Edge";
  state: "New" | "Healthy" | "Challenged" | "CoolingDown" | "Expired" | "ResetRequired";
  userDataDirectory: string;
  canLaunch: boolean;
  cooldownUntil: string | null;
  consecutiveChallenges: number;
  lastUsedAt: string | null;
  lastVerifiedAt: string | null;
}

export interface SessionList {
  items: SessionPayload[];
  total: number;
}

/** Body of POST /api/v1/sessions. */
export interface CreateSessionInput {
  name: string;
  sourceKey: string;
  engine: string;
  channel: string;
}

/** Body of POST /api/v1/search-profiles. Omitted caps default to the source policy. */
export interface CreateProfileInput {
  name: string;
  sourceKey: string;
  keywords: string;
  locations: string[];
  experienceYears: number | null;
  postedWithinDays: number | null;
  remoteOnly: boolean;
  requiredSkills: string[];
}

/** GET /api/v1/runs/{id}: a scrape run's stage, counters, and caps. Timestamps are UTC ISO. */
export interface RunPayload {
  id: string;
  profileId: string;
  profileName: string | null;
  sourceKey: string;
  stage: string;
  isTerminal: boolean;
  pagesFetched: number;
  postingsFound: number;
  postingsNew: number;
  postingsUpdated: number;
  detailsFetched: number;
  challenges: number;
  failures: number;
  maxPages: number;
  maxJobs: number;
  maxDetails: number;
  pauseReason: string | null;
  retryAt: string | null;
  failureReason: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
}

/** GET /api/v1/runs: newest first. */
export interface RunList {
  items: RunPayload[];
  total: number;
}

/** One posting a run saw. Missing source fields are null, never faked. */
export interface RunPosting {
  id: string;
  externalJobId: string;
  url: string;
  title: string;
  company: string | null;
  location: string | null;
  workMode: string;
  experience: string | null;
  salary: string | null;
  postedAt: string | null;
  skills: string[];
  enriched: boolean;
  isNew: boolean;
  firstSeenAt: string;
  lastSeenAt: string;
}

/** GET /api/v1/runs/{id}/postings. */
export interface RunPostingList {
  runId: string;
  items: RunPosting[];
}

/** Body of POST /api/v1/runs. */
export interface StartRunInput {
  profileId: string;
  browserProfileId: string;
  enrich: boolean;
}

import { renderDebugEnabled } from "./debug.ts";

/** RFC 9457 problem body plus the stable `code` and optional field `errors` extensions. */
export interface ApiProblem {
  status: number;
  title: string;
  detail: string;
  code: string;
  errors?: Record<string, string[]>;
}

export type ApiResult<T> = { ok: true; value: T } | { ok: false; problem: ApiProblem };

/** A result plus how long the round trip took and when it finished. */
export interface Timed<T> {
  result: T;
  ms: number;
  at: string;
}

export type FetchImpl = (
  input: string,
  init?: RequestInit,
) => Promise<{ ok: boolean; status: number; json(): Promise<unknown> }>;

export function createApiClient(fetchImpl: FetchImpl = fetch as unknown as FetchImpl) {
  return {
    fetchMeta: () => timed(() => get<{ status: string; upstream: string }>(fetchImpl, "/healthz")),
    fetchHealth: () => timed(() => get<HealthPayload>(fetchImpl, "/api/v1/health")),
    fetchProfile: (id: string) =>
      timed(() => get<SearchProfilePayload>(fetchImpl, `/api/v1/search-profiles/${id}`)),
    listProfiles: () => timed(() => get<SearchProfileList>(fetchImpl, "/api/v1/search-profiles")),
    listSources: () => timed(() => get<SourceList>(fetchImpl, "/api/v1/sources")),
    listSessions: () => timed(() => get<SessionList>(fetchImpl, "/api/v1/sessions")),
    createSession: (input: CreateSessionInput) =>
      timed(() => send<SessionPayload>(fetchImpl, "POST", "/api/v1/sessions", input)),
    createProfile: (input: CreateProfileInput) =>
      timed(() => send<SearchProfilePayload>(fetchImpl, "POST", "/api/v1/search-profiles", input)),
    listRuns: () => timed(() => get<RunList>(fetchImpl, "/api/v1/runs")),
    getRun: (id: string) => timed(() => get<RunPayload>(fetchImpl, `/api/v1/runs/${id}`)),
    listRunPostings: (id: string) =>
      timed(() => get<RunPostingList>(fetchImpl, `/api/v1/runs/${id}/postings`)),
    startRun: (input: StartRunInput) =>
      timed(() => send<RunPayload>(fetchImpl, "POST", "/api/v1/runs", input)),
    cancelRun: (id: string) =>
      timed(() => send<RunPayload>(fetchImpl, "POST", `/api/v1/runs/${id}/cancel`, {})),
  };
}

/** The client surface consumed by components. Passed as a prop for testability. */
export type ApiClient = ReturnType<typeof createApiClient>;

async function timed<T>(run: () => Promise<T>): Promise<Timed<T>> {
  const start = performance.now();
  const result = await run();
  return { result, ms: performance.now() - start, at: new Date().toISOString() };
}

function get<T>(fetchImpl: FetchImpl, path: string): Promise<ApiResult<T>> {
  return send<T>(fetchImpl, "GET", path);
}

async function send<T>(
  fetchImpl: FetchImpl,
  method: "GET" | "POST",
  path: string,
  body?: unknown,
): Promise<ApiResult<T>> {
  if (renderDebugEnabled) {
    console.debug(`[fetch] ${method} ${path}`);
  }
  let res: { ok: boolean; status: number; json(): Promise<unknown> };
  try {
    res =
      body === undefined
        ? await fetchImpl(path)
        : await fetchImpl(path, {
            method,
            headers: { "content-type": "application/json" },
            body: JSON.stringify(body),
          });
  } catch {
    return {
      ok: false,
      problem: {
        status: 0,
        title: "Cockpit unreachable",
        detail: "Could not reach the BFF. Is `bun run dev` running?",
        code: "BFF_UNREACHABLE",
      },
    };
  }
  if (res.ok) {
    // A 2xx that is not JSON (an HTML page from a misrouted upstream) is a
    // problem like any other, never a throw the caller has to remember.
    try {
      return { ok: true, value: (await res.json()) as T };
    } catch {
      return {
        ok: false,
        problem: {
          status: res.status,
          title: "Unreadable response",
          detail: `The server answered ${res.status} with a body that is not JSON.`,
          code: "BAD_RESPONSE",
        },
      };
    }
  }
  return { ok: false, problem: await toProblem(res) };
}

async function toProblem(res: { status: number; json(): Promise<unknown> }): Promise<ApiProblem> {
  const fallback: ApiProblem = {
    status: res.status,
    title: "Request failed",
    detail: `The server answered with status ${res.status}.`,
    code: "UNKNOWN",
  };
  try {
    const body = (await res.json()) as Record<string, unknown>;
    return {
      status: typeof body["status"] === "number" ? body["status"] : res.status,
      title: typeof body["title"] === "string" ? body["title"] : fallback.title,
      detail: typeof body["detail"] === "string" ? body["detail"] : fallback.detail,
      code: typeof body["code"] === "string" ? body["code"] : "UNKNOWN",
      ...(isFieldErrors(body["errors"]) ? { errors: body["errors"] } : {}),
    };
  } catch {
    return fallback;
  }
}

/** Field errors are a map of JSON field name to messages; anything else is ignored. */
function isFieldErrors(value: unknown): value is Record<string, string[]> {
  return (
    typeof value === "object" &&
    value !== null &&
    !Array.isArray(value) &&
    Object.values(value).every((v) => Array.isArray(v) && v.every((m) => typeof m === "string"))
  );
}
