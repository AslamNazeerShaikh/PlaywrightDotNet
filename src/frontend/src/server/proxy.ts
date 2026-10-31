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

/**
 * Joins the upstream base URL with the incoming `/api/...` path. The base is expected to have no
 * trailing slash (see `parseConfig`).
 */
export function buildTargetUrl(base: string, path: string): string {
  return `${base}${path}`;
}

/** Copies end-to-end headers, dropping hop-by-hop ones. */
export function filterHeaders(headers: Headers): Headers {
  const out = new Headers();
  headers.forEach((value, key) => {
    if (!HOP_BY_HOP.has(key.toLowerCase())) {
      out.set(key, value);
    }
  });
  return out;
}

/**
 * Forwards `req` to `base + path` and streams the upstream response back. Returns a 502
 * problem+json response when the data plane is unreachable.
 */
export async function proxyRequest(
  req: Request,
  base: string,
  path: string,
  fetchImpl: typeof fetch = fetch,
): Promise<Response> {
  let upstream: Response;
  try {
    upstream = await fetchImpl(buildTargetUrl(base, path), {
      method: req.method,
      headers: filterHeaders(req.headers),
      // GET/HEAD must not carry a body; anything else streams through.
      body:
        req.method === "GET" || req.method === "HEAD" ? undefined : req.body,
      // The BFF is not a browser: no credentials, no caching, manual redirects.
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
  return upstream;
}
