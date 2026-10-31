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

export interface SearchProfilePayload {
  id: string;
  name: string;
  sourceKey: string;
}

import { renderDebugEnabled } from "./debug.ts";

/** RFC 9457 problem body plus the stable `code` extension. */
export interface ApiProblem {
  status: number;
  title: string;
  detail: string;
  code: string;
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
  };
}

/** The client surface consumed by components. Passed as a prop for testability. */
export type ApiClient = ReturnType<typeof createApiClient>;

async function timed<T>(run: () => Promise<T>): Promise<Timed<T>> {
  const start = performance.now();
  const result = await run();
  return { result, ms: performance.now() - start, at: new Date().toISOString() };
}

async function get<T>(fetchImpl: FetchImpl, path: string): Promise<ApiResult<T>> {
  if (renderDebugEnabled) {
    console.debug(`[fetch] GET ${path}`);
  }
  let res: { ok: boolean; status: number; json(): Promise<unknown> };
  try {
    res = await fetchImpl(path);
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
    return { ok: true, value: (await res.json()) as T };
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
    };
  } catch {
    return fallback;
  }
}
