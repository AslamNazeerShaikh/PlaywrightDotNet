/**
 * Cockpit BFF entry point: loopback-only `Bun.serve` with three route groups.
 *
 * - `GET /healthz` — BFF self-check (does not touch the .NET API).
 * - `/bff/session*` — HttpOnly session cookie lifecycle.
 * - `/api/*` — proxied to the .NET data plane (see `proxy.ts`).
 * - `/*` — the React bundle.
 */
import { type CockpitConfig, parseConfig } from "./config.ts";
import {
  clearedCookie,
  parseCookies,
  readSessionToken,
  serializeCookie,
  SESSION_COOKIE_NAMES,
} from "./cookies.ts";
import { proxyRequest } from "./proxy.ts";

/** Creates the BFF request handler; `startServer` wraps it in `Bun.serve`. */
export function createHandler(config: CockpitConfig) {
  return {
    async fetch(req: Request): Promise<Response> {
      const url = new URL(req.url);

      if (url.pathname === "/healthz") {
        return healthResponse(config);
      }

      if (url.pathname === "/bff/session" || url.pathname === "/bff/session/setup") {
        return bffSession(req, config, url);
      }

      if (url.pathname === "/api" || url.pathname.startsWith("/api/")) {
        return apiProxy(req, config);
      }

      return fallbackResponse(url.pathname);
    },
  };
}

/**
 * Loads the TLS keypair when configured; undefined means plain HTTP. Throws when only one half of
 * the pair is set or either file is missing, with the fix in the message.
 */
export async function loadTls(
  config: CockpitConfig,
): Promise<{ cert: ReturnType<typeof Bun.file>; key: ReturnType<typeof Bun.file> } | undefined> {
  const certFile = config.tlsCertFile;
  const keyFile = config.tlsKeyFile;
  if (certFile === undefined && keyFile === undefined) {
    return undefined;
  }
  if (certFile === undefined || keyFile === undefined) {
    throw new Error(
      'BFF_TLS_CERT_FILE and BFF_TLS_KEY_FILE must be set together; export the dev cert first (see README "HTTPS dev loop").',
    );
  }
  const cert = Bun.file(certFile);
  const key = Bun.file(keyFile);
  if (!(await cert.exists())) {
    throw new Error(
      `BFF_TLS_CERT_FILE not found: ${certFile}. Export the dev cert first (see README "HTTPS dev loop").`,
    );
  }
  if (!(await key.exists())) {
    throw new Error(
      `BFF_TLS_KEY_FILE not found: ${keyFile}. Export the dev cert first (see README "HTTPS dev loop").`,
    );
  }
  return { cert, key };
}

/**
 * True for the direct auth endpoints the BFF hides from the browser. Case, percent-encoding, and
 * trailing slashes are normalized before the compare.
 */
export function isBlockedAuthPath(pathname: string): boolean {
  let decoded = pathname;
  try {
    decoded = decodeURIComponent(pathname);
  } catch {
    decoded = pathname;
  }
  const normalized = decoded.toLowerCase().replace(/\/+$/, "");
  return normalized === "/api/v1/auth/login" || normalized === "/api/v1/auth/setup-password";
}

/** GET /healthz answer: `ok` plus the configured upstream. Never calls the data plane. */
export function healthResponse(config: CockpitConfig): Response {
  return Response.json({ status: "ok", upstream: config.dotnetApiUrl });
}

/**
 * `/api/*` entry: blocks the hidden auth endpoints, answers unsafe calls that carry no session with
 * the routable 401, enforces the origin/CSRF guard on the rest, then proxies to the data plane.
 */
export function apiProxy(req: Request, config: CockpitConfig): Promise<Response> {
  const url = new URL(req.url);
  // Direct auth endpoints are blocked so token JSON never reaches the browser.
  if (isBlockedAuthPath(url.pathname)) {
    return Promise.resolve(
      Response.json(
        {
          type: "https://jobscrape.local/problems/not-found",
          title: "Not found",
          status: 404,
          detail: `No BFF route for ${url.pathname}.`,
          code: "BFF_NOT_FOUND",
        },
        { status: 404, headers: { "content-type": "application/problem+json" } },
      ),
    );
  }

  const unsafe = req.method !== "GET" && req.method !== "HEAD";
  if (unsafe && url.pathname.startsWith("/api/")) {
    // No session means no CSRF secret to protect: answer the routable 401 the client signs out
    // on instead of a 403 that reads like a bad form and leaves the deck looking signed in.
    if (readSessionToken(req) === null) {
      return Promise.resolve(sessionEndedResponse());
    }
    const csrf = csrfCheck(req);
    if (!csrf.ok) {
      return Promise.resolve(csrf.response);
    }
  }

  return proxyRequest(req, config.dotnetApiUrl, url.pathname + url.search);
}

interface CsrfResult {
  ok: boolean;
  response: Response;
}

function csrfCheck(req: Request): CsrfResult {
  const url = new URL(req.url);
  const origin = req.headers.get("origin");
  const expectedOrigin = `${url.protocol}//${url.host}`;
  if (origin === null || origin !== expectedOrigin) {
    return {
      ok: false,
      response: problemResponse(403, "ORIGIN_NOT_ALLOWED", "Origin not allowed."),
    };
  }
  const secFetchSite = req.headers.get("sec-fetch-site");
  if (secFetchSite !== null && secFetchSite !== "same-origin") {
    return {
      ok: false,
      response: problemResponse(403, "ORIGIN_NOT_ALLOWED", "Sec-Fetch-Site not allowed."),
    };
  }
  // Require Sec-Fetch-Site header presence for unsafe API calls.
  if (secFetchSite === null) {
    return {
      ok: false,
      response: problemResponse(403, "ORIGIN_NOT_ALLOWED", "Sec-Fetch-Site missing."),
    };
  }
  if (!csrfPairMatches(req)) {
    return { ok: false, response: problemResponse(403, "CSRF_FAILED", "CSRF token mismatch.") };
  }
  return { ok: true, response: new Response(null, { status: 204 }) };
}

/** Guard preset for a session route: bootstrap (create), read, or delete. */
export type SessionGuardMode = "bootstrap" | "read" | "delete";

function normalizeGuardMode(mode: string): SessionGuardMode {
  const m = mode.toLowerCase();
  if (m === "bootstrap" || m === "login" || m === "setup" || m === "create" || m === "post") {
    return "bootstrap";
  }
  if (m === "read" || m === "get" || m === "session" || m === "me") {
    return "read";
  }
  return "delete";
}

/**
 * Checks a session request against its mode's origin/CSRF rules. Returns the 403 problem to send,
 * or null when the request may proceed.
 */
export function guardSession(req: Request, mode: string | SessionGuardMode): Response | null {
  const url = new URL(req.url);
  const expectedOrigin = `${url.protocol}//${url.host}`;
  const origin = req.headers.get("origin");
  const secFetchSite = req.headers.get("sec-fetch-site");
  const m = normalizeGuardMode(String(mode));

  if (m === "bootstrap" || m === "delete") {
    if (origin === null || origin !== expectedOrigin) {
      return problemResponse(403, "ORIGIN_NOT_ALLOWED", "Origin not allowed.");
    }
    if (secFetchSite !== "same-origin") {
      return problemResponse(403, "ORIGIN_NOT_ALLOWED", "Sec-Fetch-Site not allowed.");
    }
  }

  if (m === "read") {
    if (origin !== null && origin !== expectedOrigin) {
      return problemResponse(403, "ORIGIN_NOT_ALLOWED", "Origin not allowed.");
    }
    if (secFetchSite !== null && secFetchSite !== "same-origin") {
      return problemResponse(403, "ORIGIN_NOT_ALLOWED", "Sec-Fetch-Site not allowed.");
    }
  }

  if (m === "delete") {
    if (!csrfPairMatches(req)) {
      return problemResponse(403, "CSRF_FAILED", "CSRF token mismatch.");
    }
  }

  return null;
}

/** True when the double-submit cookie and header are both present and equal. */
function csrfMatches(cookie: string | undefined, header: string | null): boolean {
  return (
    cookie !== undefined && cookie !== "" && header !== null && header !== "" && cookie === header
  );
}

/** Reads the CSRF pair from the request and reports whether it matches. */
function csrfPairMatches(req: Request): boolean {
  const cookies = parseCookies(req.headers.get("cookie"));
  return csrfMatches(cookies["jobscrape_csrf"], req.headers.get("x-csrf-token"));
}

/** 401 problem the client routes to sign-in when an unsafe call arrives with no session. */
function sessionEndedResponse(): Response {
  return Response.json(
    {
      type: "https://jobscrape.local/problems/unauthorized",
      title: "Unauthorized",
      status: 401,
      detail: "The session ended. Sign in again.",
      code: "UNAUTHENTICATED",
    },
    { status: 401, headers: { "content-type": "application/problem+json" } },
  );
}

function problemResponse(status: number, code: string, detail: string): Response {
  return Response.json(
    {
      type: "https://jobscrape.local/problems/forbidden",
      title: "Forbidden",
      status,
      detail,
      code,
    },
    { status, headers: { "content-type": "application/problem+json" } },
  );
}

function isSecure(req: Request, config: CockpitConfig): boolean {
  return config.tlsCertFile !== undefined || new URL(req.url).protocol === "https:";
}

function sessionCookieName(secure: boolean): string {
  return secure ? SESSION_COOKIE_NAMES[0] : SESSION_COOKIE_NAMES[1];
}

function generateCsrfToken(): string {
  return crypto.randomUUID().replace(/-/g, "");
}

async function bffSession(req: Request, config: CockpitConfig, url: URL): Promise<Response> {
  if (url.pathname === "/bff/session/setup" && req.method === "POST") {
    const blocked = guardSession(req, "bootstrap");
    if (blocked) return blocked;
    return createSessionFromUpstream(req, config, "/api/v1/auth/setup-password");
  }
  if (url.pathname === "/bff/session" && req.method === "POST") {
    const blocked = guardSession(req, "bootstrap");
    if (blocked) return blocked;
    return createSessionFromUpstream(req, config, "/api/v1/auth/login");
  }
  if (url.pathname === "/bff/session" && req.method === "GET") {
    const blocked = guardSession(req, "read");
    if (blocked) return blocked;
    return getSession(req, config);
  }
  if (url.pathname === "/bff/session" && req.method === "DELETE") {
    const blocked = guardSession(req, "delete");
    if (blocked) return blocked;
    return deleteSession(req, config);
  }
  return methodNotAllowed();
}

async function createSessionFromUpstream(
  req: Request,
  config: CockpitConfig,
  upstreamPath: string,
): Promise<Response> {
  let body: unknown;
  try {
    body = await req.json();
  } catch {
    body = {};
  }

  const upstream = await fetch(config.dotnetApiUrl + upstreamPath, {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(body),
  }).catch(() => null);

  if (upstream === null) {
    return Response.json(
      {
        type: "https://jobscrape.local/problems/dotnet-unreachable",
        title: "Data plane unreachable",
        status: 502,
        detail: "The .NET API did not answer. Start it with `dotnet run`.",
        code: "DOTNET_UNREACHABLE",
      },
      { status: 502, headers: { "content-type": "application/problem+json" } },
    );
  }

  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    let parsed: unknown = null;
    try {
      parsed = text ? JSON.parse(text) : null;
    } catch {
      parsed = null;
    }
    return new Response(text, {
      status: upstream.status,
      headers: {
        "content-type": upstream.headers.get("content-type") ?? "application/problem+json",
      },
    });
  }

  const data = (await upstream.json()) as { token: string; mustChangePassword: boolean };
  const secure = isSecure(req, config);
  const sName = sessionCookieName(secure);
  const csrf = generateCsrfToken();
  const headers = new Headers({ "content-type": "application/json" });
  headers.append("set-cookie", serializeCookie(sName, data.token, { secure, httpOnly: true }));
  headers.append(
    "set-cookie",
    serializeCookie("jobscrape_csrf", csrf, { secure, httpOnly: false }),
  );
  return new Response(JSON.stringify({ mustChangePassword: data.mustChangePassword }), {
    status: 200,
    headers,
  });
}

async function getSession(req: Request, config: CockpitConfig): Promise<Response> {
  const token = readSessionToken(req);
  if (token === null) {
    return Response.json({ authenticated: false });
  }
  const upstream = await fetch(config.dotnetApiUrl + "/api/v1/auth/me", {
    headers: { authorization: `Bearer ${token}` },
  }).catch(() => null);
  if (upstream === null) {
    return Response.json(
      {
        type: "https://jobscrape.local/problems/dotnet-unreachable",
        title: "Data plane unreachable",
        status: 502,
        detail: "The .NET API did not answer. Start it with `dotnet run`.",
        code: "DOTNET_UNREACHABLE",
      },
      { status: 502, headers: { "content-type": "application/problem+json" } },
    );
  }
  if (upstream.status === 401) {
    const text = await upstream.text().catch(() => "");
    let code: string | undefined;
    try {
      const parsed = JSON.parse(text) as { code?: unknown };
      code = typeof parsed.code === "string" ? parsed.code : undefined;
    } catch {
      code = undefined;
    }
    const secure = isSecure(req, config);
    const headers = new Headers({ "content-type": "application/json" });
    for (const name of SESSION_COOKIE_NAMES) {
      headers.append("set-cookie", clearedCookie(name, secure, true));
    }
    headers.append("set-cookie", clearedCookie("jobscrape_csrf", secure, false));
    const body =
      code === "SESSION_REVOKED"
        ? { authenticated: false, expired: true, revoked: true }
        : { authenticated: false, expired: true };
    return new Response(JSON.stringify(body), {
      status: 200,
      headers,
    });
  }
  if (!upstream.ok) {
    const text = await upstream.text().catch(() => "");
    return new Response(text, {
      status: upstream.status,
      headers: {
        "content-type": upstream.headers.get("content-type") ?? "application/problem+json",
      },
    });
  }
  const me = (await upstream.json()) as Record<string, unknown>;
  const mustChangePassword = decodeMustChangePassword(token);
  return Response.json({ ...me, mustChangePassword });
}

function deleteSession(req: Request, config: CockpitConfig): Response {
  const secure = isSecure(req, config);
  const headers = new Headers();
  for (const name of SESSION_COOKIE_NAMES) {
    headers.append("set-cookie", clearedCookie(name, secure, true));
  }
  headers.append("set-cookie", clearedCookie("jobscrape_csrf", secure, false));
  return new Response(null, { status: 204, headers });
}

function decodeMustChangePassword(token: string | null): boolean {
  if (token === null) return false;
  const parts = token.split(".");
  if (parts.length < 2) return false;
  try {
    const payload = parts[1]!.replace(/-/g, "+").replace(/_/g, "/");
    const padded = payload + "=".repeat((4 - (payload.length % 4)) % 4);
    const json = JSON.parse(atob(padded)) as Record<string, unknown>;
    const value = json["must_change_password"];
    return value === true || value === "true";
  } catch {
    return false;
  }
}

function methodNotAllowed(): Response {
  return Response.json(
    {
      type: "https://jobscrape.local/problems/method-not-allowed",
      title: "Method not allowed",
      status: 405,
      detail: "Method not allowed.",
      code: "METHOD_NOT_ALLOWED",
    },
    { status: 405, headers: { "content-type": "application/problem+json" } },
  );
}

/** 404 problem+json for a path the BFF does not own; `pathname` is echoed in the detail. */
export function fallbackResponse(pathname: string): Response {
  return Response.json(
    {
      type: "https://jobscrape.local/problems/not-found",
      title: "Not found",
      status: 404,
      detail: `No BFF route for ${pathname}.`,
      code: "BFF_NOT_FOUND",
    },
    { status: 404, headers: { "content-type": "application/problem+json" } },
  );
}

/** What the wildcard route serves: a Bun HTML bundle in dev, a request handler in production. */
export type FallbackRoute = import("bun").HTMLBundle | ((req: Request) => Promise<Response>);

/**
 * Binds the loopback server with the health, session, API, and fallback routes. Loads TLS first, so
 * a missing or half-configured keypair fails before the port opens.
 */
export async function startServer(config: CockpitConfig, fallback: FallbackRoute): Promise<void> {
  const routes = {
    "/healthz": () => healthResponse(config),
    "/bff/session": (req: Request) => bffSession(req, config, new URL(req.url)),
    "/bff/session/setup": (req: Request) => bffSession(req, config, new URL(req.url)),
    "/api/*": (req: Request) => apiProxy(req, config),
    "/*": fallback,
  };
  const tls = await loadTls(config);
  const scheme = tls === undefined ? "http" : "https";
  Bun.serve({
    port: config.port,
    hostname: "127.0.0.1",
    ...(tls !== undefined ? { tls } : {}),
    routes,
  });
  console.log(`jobscrape-cockpit BFF listening on ${scheme}://127.0.0.1:${config.port}`);
}

if (import.meta.main) {
  const config = parseConfig(Bun.env);
  await startServer(config, serveDist);
}

async function serveDist(req: Request): Promise<Response> {
  const url = new URL(req.url);
  const root = new URL("../../dist/client/", import.meta.url);
  const rel = url.pathname.replace(/^\/+/, "");
  const file = Bun.file(new URL(rel === "" ? "index.html" : rel, root));
  if (await file.exists()) {
    return new Response(file);
  }
  const fallback = Bun.file(new URL("index.html", root));
  if (await fallback.exists()) {
    return new Response(fallback);
  }
  return new Response("Cockpit client not built. Run `bun run build:client` first.", {
    status: 503,
  });
}
