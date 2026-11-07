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
  companies: string[];
  maxPages: number;
  startPage: number;
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
    runCap: number | null;
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

/** Body of PATCH /api/v1/sessions/{id}. The source is immutable. */
export interface UpdateSessionInput {
  name: string;
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
  companies: string[];
}

/** Body of PUT /api/v1/search-profiles/{id}. The source is immutable. */
export interface UpdateProfileInput {
  name: string;
  keywords: string;
  locations: string[];
  experienceYears: number | null;
  postedWithinDays: number | null;
  remoteOnly: boolean;
  requiredSkills: string[];
  companies: string[];
  maxPages: number | null;
  startPage: number | null;
  maxJobs: number | null;
  maxDetails: number | null;
  minDelayMs: number | null;
  maxDelayMs: number | null;
  enabled: boolean;
}

/** GET /api/v1/runs/{id}: a scrape run's stage, counters, and caps. Timestamps are UTC ISO. */
export interface RunPayload {
  id: string;
  profileId: string;
  profileName: string | null;
  companies: string[];
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
  startPage: number;
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
  total: number;
}

/** Body of POST /api/v1/runs. */
export interface StartRunInput {
  profileId: string;
  browserProfileId: string;
  enrich: boolean;
}

/** Export file shape: csv streams text, xlsx is a workbook. */
export type ExportFormat = "csv" | "xlsx";

/** Which postings an export covers: everything, only the profile companies, or all but them. */
export type ExportGroup = "all" | "only" | "exclude";

/** A successful sign-in: the bearer token plus whether first login is still pending. */
export interface LoginResponse {
  token: string;
  mustChangePassword: boolean;
}

/** The signed-in caller as the token describes them. */
export interface MeResponse {
  email: string;
  roles: string[];
  permissions: string[];
}

/** One user for the admin surface: identity plus assigned role names. */
export interface UserResponse {
  id: string;
  email: string;
  mustChangePassword: boolean;
  roles: string[];
}

export interface UserListResponse {
  users: UserResponse[];
}

/** One role for the admin surface: name plus granted permission keys. */
export interface RoleResponse {
  id: string;
  name: string;
  permissions: string[];
}

export interface RoleListResponse {
  roles: RoleResponse[];
}

/** One usage quota definition: the governed scope plus limit and window. */
export interface QuotaResponse {
  scope: string;
  limit: number;
  /** Fixed window as the API renders it, e.g. "00:01:00". */
  window: string;
}

export interface QuotaListResponse {
  quotas: QuotaResponse[];
}

/** The stable v1 permission set, in catalog order (see PermissionCatalog). */
export const PERMISSION_CATALOG = [
  "profiles.read",
  "profiles.write",
  "runs.read",
  "runs.write",
  "users.read",
  "users.write",
  "roles.read",
  "roles.write",
  "sessions.read",
  "sessions.write",
  "sources.read",
] as const;

export type PermissionKey = (typeof PERMISSION_CATALOG)[number];

/** Body of POST /api/v1/auth/login. */
export interface LoginInput {
  email: string;
  password: string;
}

/** Body of POST /api/v1/auth/setup-password. */
export interface SetupPasswordInput {
  email: string;
  currentSecret: string;
  newPassword: string;
}

/** Body of POST /api/v1/auth/change-password. */
export interface ChangePasswordInput {
  currentPassword: string;
  newPassword: string;
}

/** Body of POST /api/v1/users. */
export interface CreateUserInput {
  email: string;
  temporaryPassword: string;
}

/** Body of POST /api/v1/roles. */
export interface CreateRoleInput {
  name: string;
}

/** Body of PUT /api/v1/quotas/{scope}. */
export interface UpdateQuotaInput {
  limit: number;
  window: string;
}

import { renderDebugEnabled } from "./debug.ts";

/** RFC 9457 problem body plus the stable `code`, optional field `errors`, and 429 backoff. */
export interface ApiProblem {
  status: number;
  title: string;
  detail: string;
  code: string;
  errors?: Record<string, string[]>;
  /** Seconds from the upstream `Retry-After` header on 429 answers, else null. */
  retryAfter?: number | null;
}

export type ApiResult<T> = { ok: true; value: T } | { ok: false; problem: ApiProblem };

/** A result plus how long the round trip took and when it finished. */
export interface Timed<T> {
  result: T;
  ms: number;
  at: string;
}

export interface DbTableInfo {
  name: string;
  rowCount: number;
}

export interface DbColumnInfo {
  name: string;
  type: string;
  notNull: boolean;
  primaryKey: boolean;
  defaultValue: string | null;
}

export interface DbForeignKeyInfo {
  column: string;
  referencedTable: string;
  referencedColumn: string;
}

export interface DbTableSchema {
  name: string;
  columns: DbColumnInfo[];
  foreignKeys: DbForeignKeyInfo[];
  primaryKeyColumn: string;
}

export interface DbRowsResult {
  rows: Record<string, unknown>[];
  total: number;
}

export type FetchImpl = (
  input: string,
  init?: RequestInit,
) => Promise<{
  ok: boolean;
  status: number;
  json(): Promise<unknown>;
  text?(): Promise<string>;
  blob?(): Promise<Blob>;
  headers?: { get(name: string): string | null };
}>;

export function createApiClient(fetchImpl: FetchImpl = fetch as unknown as FetchImpl) {
  const authed = <T>(
    method: "GET" | "POST" | "PATCH" | "PUT" | "DELETE",
    path: string,
    body?: unknown,
    idempotencyKey?: string,
  ): Promise<ApiResult<T>> => {
    return send<T>(fetchImpl, method, path, body, idempotencyKey);
  };
  return {
    fetchMeta: () => timed(() => get<{ status: string; upstream: string }>(fetchImpl, "/healthz")),
    fetchHealth: () => timed(() => get<HealthPayload>(fetchImpl, "/api/v1/health")),
    login: (input: LoginInput) =>
      timed(() => send<{ mustChangePassword: boolean }>(fetchImpl, "POST", "/bff/session", input)),
    setupPassword: (input: SetupPasswordInput) =>
      timed(() =>
        send<{ mustChangePassword: boolean }>(fetchImpl, "POST", "/bff/session/setup", input),
      ),
    fetchSession: () =>
      timed(() =>
        send<MeResponse | { authenticated: false; expired?: boolean }>(
          fetchImpl,
          "GET",
          "/bff/session",
        ),
      ),
    signOut: () => timed(() => send<void>(fetchImpl, "DELETE", "/bff/session")),
    changePassword: (input: ChangePasswordInput) =>
      timed(() => authed<void>("POST", "/api/v1/auth/change-password", input)),
    fetchMe: () => timed(() => authed<MeResponse>("GET", "/api/v1/auth/me")),
    listUsers: () => timed(() => authed<UserListResponse>("GET", "/api/v1/users")),
    getUser: (id: string) => timed(() => authed<UserResponse>("GET", `/api/v1/users/${id}`)),
    createUser: (input: CreateUserInput, idempotencyKey?: string) =>
      timed(() => authed<{ id: string }>("POST", "/api/v1/users", input, idempotencyKey)),
    assignRole: (id: string, role: string) =>
      timed(() => authed<void>("POST", `/api/v1/users/${id}/roles`, { role })),
    revokeRole: (id: string, role: string) =>
      timed(() => authed<void>("DELETE", `/api/v1/users/${id}/roles/${role}`)),
    listRoles: () => timed(() => authed<RoleListResponse>("GET", "/api/v1/roles")),
    getRole: (name: string) => timed(() => authed<RoleResponse>("GET", `/api/v1/roles/${name}`)),
    createRole: (input: CreateRoleInput, idempotencyKey?: string) =>
      timed(() => authed<{ id: string }>("POST", "/api/v1/roles", input, idempotencyKey)),
    grantPermission: (name: string, key: string) =>
      timed(() => authed<void>("POST", `/api/v1/roles/${name}/permissions`, { key })),
    revokePermission: (name: string, key: string) =>
      timed(() => authed<void>("DELETE", `/api/v1/roles/${name}/permissions/${key}`)),
    listQuotas: () => timed(() => authed<QuotaListResponse>("GET", "/api/v1/quotas")),
    updateQuota: (scope: string, input: UpdateQuotaInput) =>
      timed(() => authed<QuotaResponse>("PUT", `/api/v1/quotas/${scope}`, input)),
    listProfiles: () => timed(() => authed<SearchProfileList>("GET", "/api/v1/search-profiles")),
    listSources: () => timed(() => authed<SourceList>("GET", "/api/v1/sources")),
    listSessions: () => timed(() => authed<SessionList>("GET", "/api/v1/sessions")),
    createSession: (input: CreateSessionInput) =>
      timed(() => authed<SessionPayload>("POST", "/api/v1/sessions", input)),
    updateSession: (id: string, input: UpdateSessionInput) =>
      timed(() => authed<SessionPayload>("PATCH", `/api/v1/sessions/${id}`, input)),
    createProfile: (input: CreateProfileInput) =>
      timed(() => authed<SearchProfilePayload>("POST", "/api/v1/search-profiles", input)),
    updateProfile: (id: string, input: UpdateProfileInput) =>
      timed(() => authed<SearchProfilePayload>("PUT", `/api/v1/search-profiles/${id}`, input)),
    listRuns: (offset = 0, limit = 50) =>
      timed(() => authed<RunList>("GET", `/api/v1/runs?offset=${offset}&limit=${limit}`)),
    getRun: (id: string) => timed(() => authed<RunPayload>("GET", `/api/v1/runs/${id}`)),
    listRunPostings: (id: string, offset = 0, limit = 200) =>
      timed(() =>
        authed<RunPostingList>(
          "GET",
          `/api/v1/runs/${id}/postings?offset=${offset}&limit=${limit}`,
        ),
      ),
    downloadRunExport: (id: string, format: ExportFormat, group: ExportGroup = "all") =>
      timed(() => downloadExport(fetchImpl, id, format, group)),
    startRun: (input: StartRunInput) =>
      timed(() => authed<RunPayload>("POST", "/api/v1/runs", input)),
    cancelRun: (id: string) =>
      timed(() => authed<RunPayload>("POST", `/api/v1/runs/${id}/cancel`, {})),
    listDbTables: () => timed(() => authed<{ items: DbTableInfo[] }>("GET", "/api/v1/db/tables")),
    getDbSchema: (table: string) =>
      timed(() =>
        authed<DbTableSchema>("GET", `/api/v1/db/tables/${encodeURIComponent(table)}/schema`),
      ),
    listDbRows: (table: string, offset: number, limit: number) =>
      timed(() =>
        authed<DbRowsResult>(
          "GET",
          `/api/v1/db/tables/${encodeURIComponent(table)}/rows?offset=${offset}&limit=${limit}`,
        ),
      ),
    insertDbRow: (table: string, values: Record<string, unknown>) =>
      timed(() =>
        authed<null>("POST", `/api/v1/db/tables/${encodeURIComponent(table)}/rows`, values),
      ),
    updateDbRow: (table: string, pk: string, values: Record<string, unknown>) =>
      timed(() =>
        authed<null>("PUT", `/api/v1/db/tables/${encodeURIComponent(table)}/rows`, {
          primaryKeyValue: pk,
          values,
        }),
      ),
    deleteDbRow: (table: string, pk: string) =>
      timed(() =>
        authed<null>(
          "DELETE",
          `/api/v1/db/tables/${encodeURIComponent(table)}/rows/${encodeURIComponent(pk)}`,
        ),
      ),
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

/**
 * Downloads a run export as a Blob. The body is binary (xlsx) or text (csv), never JSON, so it
 * bypasses `send`: failures still surface as problems via the shared `toProblem` mapping.
 */
async function downloadExport(
  fetchImpl: FetchImpl,
  id: string,
  format: ExportFormat,
  group: ExportGroup,
): Promise<ApiResult<{ blob: Blob; filename: string }>> {
  const query = group === "all" ? `format=${format}` : `format=${format}&group=${group}`;
  const path = `/api/v1/runs/${id}/postings/export?${query}`;
  const headers: Record<string, string> = {};
  const csrf = readCsrfToken();
  if (csrf) headers["x-csrf-token"] = csrf;
  const init: RequestInit = { method: "GET", credentials: "same-origin" };
  if (Object.keys(headers).length > 0) init.headers = headers;
  let res: Awaited<ReturnType<FetchImpl>>;
  try {
    res = await fetchImpl(path, init);
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
  if (!res.ok) {
    return { ok: false, problem: await toProblem(res) };
  }
  if (typeof res.blob !== "function") {
    return {
      ok: false,
      problem: {
        status: res.status,
        title: "Unreadable response",
        detail: "The export answer has no downloadable body.",
        code: "BAD_RESPONSE",
      },
    };
  }
  let blob: Blob;
  try {
    blob = await res.blob();
  } catch {
    return {
      ok: false,
      problem: {
        status: res.status,
        title: "Unreadable response",
        detail: "The export body could not be read.",
        code: "BAD_RESPONSE",
      },
    };
  }
  return { ok: true, value: { blob, filename: exportFilename(res.headers, id, format) } };
}

/**
 * The Content-Disposition filename when the server sends one, else the stable fallback
 * `run-postings-{id}.{format}`.
 */
function exportFilename(
  headers: { get(name: string): string | null } | undefined,
  id: string,
  format: ExportFormat,
): string {
  const raw = headers?.get("content-disposition") ?? null;
  if (raw !== null) {
    const match = /filename\*?=(?:UTF-8''|")?([^";\s]+)/i.exec(raw);
    if (match?.[1] !== undefined) {
      const name = match[1].replace(/"$/, "");
      try {
        return decodeURIComponent(name);
      } catch {
        return name;
      }
    }
  }
  return `run-postings-${id}.${format}`;
}

async function send<T>(
  fetchImpl: FetchImpl,
  method: "GET" | "POST" | "PATCH" | "PUT" | "DELETE",
  path: string,
  body?: unknown,
  idempotencyKey?: string,
): Promise<ApiResult<T>> {
  if (renderDebugEnabled) {
    // Method and path only: bodies can carry secrets, tokens never appear here.
    console.debug(`[fetch] ${method} ${path}`);
  }
  let res: {
    ok: boolean;
    status: number;
    json(): Promise<unknown>;
    text?(): Promise<string>;
    headers?: { get(name: string): string | null };
  };
  try {
    // The method always travels, even without a body. A bare
    // `fetchImpl(path)` would silently downgrade DELETE to GET.
    const headers: Record<string, string> = {};
    if (body !== undefined) {
      headers["content-type"] = "application/json";
    }
    if (idempotencyKey !== undefined && idempotencyKey !== "") {
      headers["idempotency-key"] = idempotencyKey;
    }
    const unsafe = method !== "GET";
    if (unsafe) {
      const csrf = readCsrfToken();
      if (csrf) headers["x-csrf-token"] = csrf;
    }
    // No headers on anonymous reads: the wire keeps its old shape exactly.
    const init: RequestInit = { method, credentials: "same-origin" };
    if (Object.keys(headers).length > 0) {
      init.headers = headers;
    }
    res =
      body === undefined
        ? await fetchImpl(path, init)
        : await fetchImpl(path, { ...init, body: JSON.stringify(body) });
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
    // 204 No Content (and any empty body) carries no JSON; treat as success.
    if (res.status === 204) {
      return { ok: true, value: undefined as T };
    }
    // A 2xx that is not JSON (an HTML page from a misrouted upstream) is a
    // problem like any other, never a throw the caller has to remember.
    // Prefer res.text() when available (real fetch / bun), else fall back to res.json().
    if (typeof res.text === "function") {
      let text = "";
      try {
        text = (await res.text()) ?? "";
      } catch {
        text = "";
      }
      if (text.trim() === "") {
        return { ok: true, value: undefined as T };
      }
      try {
        return { ok: true, value: JSON.parse(text) as T };
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

async function toProblem(res: {
  status: number;
  json(): Promise<unknown>;
  headers?: { get(name: string): string | null };
}): Promise<ApiProblem> {
  const fallback: ApiProblem = {
    status: res.status,
    title: "Request failed",
    detail: `The server answered with status ${res.status}.`,
    code: "UNKNOWN",
  };
  // 429 answers carry Retry-After; the page renders the backoff from this number.
  const retryAfter = parseRetryAfter(res.headers?.get("retry-after") ?? null);
  try {
    const body = (await res.json()) as Record<string, unknown>;
    return {
      status: typeof body["status"] === "number" ? body["status"] : res.status,
      title: typeof body["title"] === "string" ? body["title"] : fallback.title,
      detail: typeof body["detail"] === "string" ? body["detail"] : fallback.detail,
      code: typeof body["code"] === "string" ? body["code"] : "UNKNOWN",
      ...(isFieldErrors(body["errors"]) ? { errors: body["errors"] } : {}),
      ...(retryAfter !== null ? { retryAfter } : {}),
    };
  } catch {
    return retryAfter !== null ? { ...fallback, retryAfter } : fallback;
  }
}

/** Backoff renders are capped at a day so absurd header values cannot reach the page. */
const MAX_RETRY_AFTER_SECONDS = 86400;

/**
 * Parses a Retry-After header value to whole seconds from now, else null. Accepts delta-seconds and
 * HTTP-date (RFC 9110 §15.5.3); a negative delta-seconds clamps to 0, a past HTTP-date yields 0,
 * and any value above MAX_RETRY_AFTER_SECONDS clamps to the cap.
 */
function parseRetryAfter(raw: string | null): number | null {
  if (raw === null) {
    return null;
  }
  const value = raw.trim();
  if (value === "") {
    return null;
  }
  if (/^-?\d+$/.test(value)) {
    const seconds = Number(value);
    return Math.min(MAX_RETRY_AFTER_SECONDS, Math.max(0, seconds));
  }
  const at = Date.parse(value);
  if (Number.isNaN(at)) {
    return null;
  }
  return Math.min(MAX_RETRY_AFTER_SECONDS, Math.max(0, Math.ceil((at - Date.now()) / 1000)));
}

/** Reads the CSRF double-submit token from the (JS-readable) cookie, or null. */
function readCsrfToken(): string | null {
  if (typeof document === "undefined") {
    return null;
  }
  const match = document.cookie.match(/(?:^|; )jobscrape_csrf=([^;]*)/);
  if (match === null || match[1] === undefined) {
    return null;
  }
  try {
    return decodeURIComponent(match[1]);
  } catch {
    return match[1];
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
