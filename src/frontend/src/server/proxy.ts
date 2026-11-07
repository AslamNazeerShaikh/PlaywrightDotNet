/**
 * Same-origin `/api/*` proxy to the .NET data plane. The cockpit never talks to the API
 * cross-origin: the browser hits the BFF, the BFF forwards loopback-only. On connection failure the
 * BFF answers 502 with a problem+json body (never a stack trace), mirroring the API edge contract.
 */
/** Hop-by-hop headers must not be forwarded to the upstream. See RFC 9110 §7.6. */
const HOP_BY_HOP = new Set([
  "connection",
  "keep-alive",
  "proxy-authenticate",
  "proxy-authorization",
  "te",
  "trailer",
  "transfer-encoding",
  "upgrade",
]);

/** Headers that carry ambient browser credentials and must never reach the upstream. */
const STRIPPED = new Set(["cookie", "authorization", "set-cookie"]);

export function buildTargetUrl(base: string, path: string): string {
  return `${base}${path}`;
}

/** Copies end-to-end headers, dropping hop-by-hop and ambient credential headers. */
export function filterHeaders(headers: Headers): Headers {
  const out = new Headers();
  headers.forEach((value, key) => {
    if (HOP_BY_HOP.has(key.toLowerCase()) || STRIPPED.has(key.toLowerCase())) {
      return;
    }
    out.set(key, value);
  });
  return out;
}

/** Parses a `Cookie` header into a name->value map (Bun 1.4 has no cookie helper). */
export function parseCookies(header: string | null): Record<string, string> {
  const out: Record<string, string> = {};
  if (header === null || header === "") {
    return out;
  }
  for (const part of header.split(";")) {
    const idx = part.indexOf("=");
    if (idx < 0) {
      continue;
    }
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

const SESSION_COOKIE_NAMES = ["__Host-jobscrape_session", "jobscrape_session"];

export function readSessionToken(req: Request): string | null {
  const cookies = parseCookies(req.headers.get("cookie"));
  for (const name of SESSION_COOKIE_NAMES) {
    const value = cookies[name];
    if (value !== undefined && value !== "") {
      return value;
    }
  }
  return null;
}

function clearCookieHeader(name: string, secure: boolean): string {
  const parts = [`${name}=`, "Path=/", "Max-Age=0", "Expires=Thu, 01 Jan 1970 00:00:00 GMT"];
  if (name.startsWith("__Host-")) {
    parts.push("Secure");
  } else if (secure) {
    parts.push("Secure");
  }
  parts.push("HttpOnly", "SameSite=Strict");
  return parts.join("; ");
}

export async function proxyRequest(
  req: Request,
  base: string,
  path: string,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  const headers = filterHeaders(req.headers);
  const token = readSessionToken(req);
  if (token !== null) {
    headers.set("authorization", `Bearer ${token}`);
  }

  let upstream: Response;
  try {
    upstream = await fetchImpl(buildTargetUrl(base, path), {
      method: req.method,
      headers,
      body: req.method === "GET" || req.method === "HEAD" ? undefined : req.body,
      redirect: "manual",
    });
  } catch (error) {
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

  const outHeaders = new Headers(upstream.headers);
  outHeaders.delete("set-cookie");

  if (upstream.status === 401) {
    // Read the body once: only UNAUTHENTICATED or SESSION_REVOKED (JWT challenge, token death)
    // clears the session. INVALID_CREDENTIALS and friends keep the session.
    const text = await upstream.text();
    let code: string | undefined;
    try {
      const parsed = JSON.parse(text) as { code?: unknown };
      code = typeof parsed.code === "string" ? parsed.code : undefined;
    } catch {
      code = undefined;
    }
    if (code === "UNAUTHENTICATED" || code === "SESSION_REVOKED") {
      const secure = new URL(req.url).protocol === "https:";
      for (const name of SESSION_COOKIE_NAMES) {
        outHeaders.append("set-cookie", clearCookieHeader(name, secure));
      }
      outHeaders.append("set-cookie", clearCookieHeader("jobscrape_csrf", secure));
    }
    outHeaders.delete("content-length");
    return new Response(text, {
      status: upstream.status,
      statusText: upstream.statusText,
      headers: outHeaders,
    });
  }

  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: outHeaders,
  });
}
