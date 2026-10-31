/**
 * Cockpit BFF entry point: loopback-only `Bun.serve` with three route groups.
 *
 * - `GET /healthz` — BFF self-check (does not touch the .NET API).
 * - `/api/*` — proxied to the .NET data plane (see `proxy.ts`).
 * - `/*` — the React bundle: the `dist/client` build output in production, or the on-demand Bun HTML
 *   import with HMR in development.
 *
 * Auth (HttpOnly cookie, CSRF double-submit, BFF bearer token) and the SSE run-event proxy land
 * with the runs vertical slice — see README follow-ups.
 */
import homepage from "../client/index.html";
import { type CockpitConfig, parseConfig } from "./config.ts";
import { proxyRequest } from "./proxy.ts";

export function createHandler(config: CockpitConfig) {
  return {
    async fetch(req: Request): Promise<Response> {
      const url = new URL(req.url);

      if (url.pathname === "/healthz") {
        return healthResponse(config);
      }

      if (url.pathname === "/api" || url.pathname.startsWith("/api/")) {
        return apiProxy(req, config);
      }

      return fallbackResponse(url.pathname);
    },
  };
}

/** BFF self-check. Never touches the .NET API. */
export function healthResponse(config: CockpitConfig): Response {
  return Response.json({ status: "ok", upstream: config.dotnetApiUrl });
}

/** Forwards an `/api/*` request to the data plane (path + query preserved). */
export function apiProxy(
  req: Request,
  config: CockpitConfig,
): Promise<Response> {
  const url = new URL(req.url);
  return proxyRequest(req, config.dotnetApiUrl, url.pathname + url.search);
}

/**
 * Stable-code 404 for unknown BFF paths. Browser navigation normally lands on the /* SPA route
 * instead (dev homepage or prod dist fallback); this stays the tested default of the routing
 * function for non-SPA clients.
 */
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

// Static-asset + SPA-fallback routes resolve differently per environment:
// production serves the committed build output, development uses Bun's HTML
// import so HMR works without a build step.
const isProduction = process.env["NODE_ENV"] === "production";

if (import.meta.main) {
  const config = parseConfig(Bun.env as Record<string, string | undefined>);
  // Bun matches `routes` before `fetch`: explicit entries keep /healthz and
  // /api/* from falling into the /* SPA wildcard. Both delegate to the same
  // responders `createHandler` uses, so route behavior stays unit-tested.
  const routes = {
    "/healthz": () => healthResponse(config),
    "/api/*": (req: Request) => apiProxy(req, config),
    "/*": isProduction ? serveDist : homepage,
  };

  Bun.serve({ port: config.port, hostname: "127.0.0.1", routes });
  console.log(
    `jobscrape-cockpit BFF listening on http://127.0.0.1:${config.port}`,
  );
}

/** Serves `dist/client` with SPA fallback to `index.html`. Production only. */
async function serveDist(req: Request): Promise<Response> {
  const url = new URL(req.url);
  const root = new URL("../../dist/client/", import.meta.url);
  // Normalize and contain: never serve outside dist/client.
  const rel = url.pathname.replace(/^\/+/, "");
  const file = Bun.file(new URL(rel === "" ? "index.html" : rel, root));
  if (await file.exists()) {
    return new Response(file);
  }
  const fallback = Bun.file(new URL("index.html", root));
  if (await fallback.exists()) {
    return new Response(fallback);
  }
  return new Response(
    "Cockpit client not built. Run `bun run build:client` first.",
    {
      status: 503,
    },
  );
}
