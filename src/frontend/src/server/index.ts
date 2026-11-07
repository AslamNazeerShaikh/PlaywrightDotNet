/**
 * Cockpit BFF entry point: loopback-only `Bun.serve` with three route groups.
 *
 * - `GET /healthz` — BFF self-check (does not touch the .NET API).
 * - `/bff/session*` — HttpOnly session cookie lifecycle.
 * - `/api/*` — proxied to the .NET data plane (see `proxy.ts`).
 * - `/*` — the React bundle.
 */
import { type CockpitConfig, parseConfig } from "./config.ts";
import { proxyRequest } from "./proxy.ts";

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

export async function loadTls(
  config: CockpitConfig,
): Promise<{ cert: ReturnType<typeof Bun.file>; key: ReturnType<typeof Bun.file> } | undefined> {
  if (config.tlsCertFile === undefined && config.tlsKeyFile === undefined) {
    return undefined;
  }
  const cert = Bun.file(config.tlsCertFile as string);
  const key = Bun.file(config.tlsKeyFile as string);
  if (!(await cert.exists())) {
    throw new Error(
      `BFF_TLS_CERT_FILE not found: ${config.tlsCertFile}. Export the dev cert first (see README "HTTPS dev loop").`,
    );
  }
  if (!(await key.exists())) {
    throw new Error(
      `BFF_TLS_KEY_FILE not found: ${config.tlsKeyFile}. Export the dev cert first (see README "HTTPS dev loop").`,
    );
  }
  return { cert, key };
}

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

export function healthResponse(config: CockpitConfig): Response {
  return Response.json({ status: "ok", upstream: config.dotnetApiUrl });
}

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
  const cookies = parseCookies(req.headers.get("cookie"));
  const csrfCookie = cookies["jobscrape_csrf"];
  const csrfHeader = req.headers.get("x-csrf-token");
  if (
    csrfCookie === undefined ||
    csrfCookie === "" ||
    csrfHeader === null ||
    csrfHeader === "" ||
    csrfCookie !== csrfHeader
  ) {
    return { ok: false, response: problemResponse(403, "CSRF_FAILED", "CSRF token mismatch.") };
  }
  return { ok: true, response: new Response(null, { status: 204 }) };
}

function parseCookies(header: string | null): Record<string, string> {
  const out: Record<string, string> = {};
  if (header === null || header === "") return out;
  for (const part of header.split(";")) {
    const idx = part.indexOf("=");
    if (idx < 0) continue;
    const name = part.slice(0, idx).trim();
    const value = part.slice(idx + 1).trim();
    if (name !== "") {
      try {
        out[name] = decodeURIComponent(value);
      } catch {
        out[name] = value;
      }
    }
  }
  return out;
}

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
    const cookies = parseCookies(req.headers.get("cookie"));
    const csrfCookie = cookies["jobscrape_csrf"];
    const csrfHeader = req.headers.get("x-csrf-token");
    if (
      csrfCookie === undefined ||
      csrfCookie === "" ||
      csrfHeader === null ||
      csrfHeader === "" ||
      csrfCookie !== csrfHeader
    ) {
      return problemResponse(403, "CSRF_FAILED", "CSRF token mismatch.");
    }
  }

  return null;
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
  return secure ? "__Host-jobscrape_session" : "jobscrape_session";
}

function buildSetCookie(
  name: string,
  value: string,
  secure: boolean,
  httpOnly: boolean,
  maxAge?: number,
): string {
  const parts = [`${name}=${value}`];
  parts.push("Path=/");
  if (name.startsWith("__Host-")) {
    parts.push("Secure");
  } else if (secure) {
    parts.push("Secure");
  }
  if (httpOnly) parts.push("HttpOnly");
  parts.push("SameSite=Strict");
  if (maxAge !== undefined) parts.push(`Max-Age=${maxAge}`);
  return parts.join("; ");
}

function clearSetCookie(name: string, secure: boolean, httpOnly: boolean): string {
  const parts = [`${name}=`, "Path=/", "Max-Age=0", "Expires=Thu, 01 Jan 1970 00:00:00 GMT"];
  if (name.startsWith("__Host-")) {
    parts.push("Secure");
  } else if (secure) {
    parts.push("Secure");
  }
  if (httpOnly) parts.push("HttpOnly");
  parts.push("SameSite=Strict");
  return parts.join("; ");
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
  headers.append("set-cookie", buildSetCookie(sName, data.token, secure, true));
  headers.append("set-cookie", buildSetCookie("jobscrape_csrf", csrf, secure, false));
  return new Response(JSON.stringify({ mustChangePassword: data.mustChangePassword }), {
    status: 200,
    headers,
  });
}

async function getSession(req: Request, config: CockpitConfig): Promise<Response> {
  const token = readSessionTokenFromReq(req);
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
    for (const name of ["__Host-jobscrape_session", "jobscrape_session"]) {
      headers.append("set-cookie", clearSetCookie(name, secure, true));
    }
    headers.append("set-cookie", clearSetCookie("jobscrape_csrf", secure, false));
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
  for (const name of ["__Host-jobscrape_session", "jobscrape_session"]) {
    headers.append("set-cookie", clearSetCookie(name, secure, true));
  }
  headers.append("set-cookie", clearSetCookie("jobscrape_csrf", secure, false));
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

function readSessionTokenFromReq(req: Request): string | null {
  const cookies = parseCookies(req.headers.get("cookie"));
  for (const name of ["__Host-jobscrape_session", "jobscrape_session"]) {
    const value = cookies[name];
    if (value !== undefined && value !== "") return value;
  }
  return null;
}

function methodNotAllowed(): Response {
  return Response.json(
    {
      type: "https://jobscrape.local/problems/not-found",
      title: "Method not allowed",
      status: 405,
      detail: "Method not allowed.",
      code: "METHOD_NOT_ALLOWED",
    },
    { status: 405, headers: { "content-type": "application/problem+json" } },
  );
}

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

export type FallbackRoute = import("bun").HTMLBundle | ((req: Request) => Promise<Response>);

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
  const config = parseConfig(Bun.env as Record<string, string | undefined>);
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
