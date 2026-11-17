/**
 * Same-origin `/api/*` proxy to the .NET data plane. The cockpit never talks to the API
 * cross-origin: the browser hits the BFF, the BFF forwards loopback-only. On connection failure the
 * BFF answers 502 with a problem+json body (never a stack trace), mirroring the API edge contract.
 */
import { clearedCookie, readSessionToken, SESSION_COOKIE_NAMES } from "./cookies.ts";

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

/** Joins the loopback base with the proxy path; the path already carries its query string. */
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

/** Proxies one request upstream, forwarding the session as a bearer and mirroring the answer. */
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
  } catch {
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
        outHeaders.append("set-cookie", clearedCookie(name, secure, true));
      }
      outHeaders.append("set-cookie", clearedCookie("jobscrape_csrf", secure, false));
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
