import { afterEach, describe, expect, test } from "bun:test";

import { buildTargetUrl, filterHeaders, proxyRequest } from "../../server/proxy.ts";

let servers: Array<{ stop(): void }> = [];

afterEach(() => {
  for (const server of servers) {
    server.stop();
  }
  servers = [];
});

describe("buildTargetUrl", () => {
  test("joins base and path, preserving the query string", () => {
    expect(buildTargetUrl("http://127.0.0.1:5207", "/api/v1/health")).toBe(
      "http://127.0.0.1:5207/api/v1/health",
    );
  });
});

describe("filterHeaders", () => {
  test("drops hop-by-hop headers and keeps end-to-end ones", () => {
    const incoming = new Headers({
      connection: "keep-alive",
      "transfer-encoding": "chunked",
      "content-type": "application/json",
      "x-request-id": "abc",
    });

    const forwarded = filterHeaders(incoming);

    expect(forwarded.get("connection")).toBeNull();
    expect(forwarded.get("transfer-encoding")).toBeNull();
    expect(forwarded.get("content-type")).toBe("application/json");
    expect(forwarded.get("x-request-id")).toBe("abc");
  });
});

describe("proxyRequest", () => {
  test("streams the upstream response back", async () => {
    const upstream = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: () =>
        Response.json(
          { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" },
          { headers: { "x-request-id": "abc" } },
        ),
    });
    servers.push(upstream);

    const res = await proxyRequest(
      new Request("http://bff/api/v1/health"),
      `http://127.0.0.1:${upstream.port}`,
      "/api/v1/health",
    );

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({
      status: "healthy",
      timeUtc: "2026-10-31T00:00:00Z",
    });
  });

  test("answers 502 problem+json when the data plane is down", async () => {
    // Port 1 is never bound: connection refused, no stub server needed.
    const res = await proxyRequest(
      new Request("http://bff/api/v1/health"),
      "http://127.0.0.1:1",
      "/api/v1/health",
    );

    expect(res.status).toBe(502);
    expect(res.headers.get("content-type")).toContain("application/problem+json");
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("DOTNET_UNREACHABLE");
  });

  test("forwards bearer and idempotency headers without logging them", async () => {
    let seenAuth = "";
    let seenKey = "";
    let seenCookie: string | null = "x";
    const upstream = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: (req) => {
        seenAuth = req.headers.get("authorization") ?? "";
        seenKey = req.headers.get("idempotency-key") ?? "";
        seenCookie = req.headers.get("cookie");
        return Response.json({ ok: true });
      },
    });
    servers.push(upstream);

    const res = await proxyRequest(
      new Request("http://bff/api/v1/users", {
        method: "POST",
        headers: {
          authorization: "Bearer tok-1",
          cookie: "jobscrape_session=jwt-9; jobscrape_csrf=csrf-9",
          "idempotency-key": "key-9",
          connection: "keep-alive",
        },
        body: "{}",
      }),
      `http://127.0.0.1:${upstream.port}`,
      "/api/v1/users",
    );

    expect(res.status).toBe(200);
    expect(seenAuth).toBe("Bearer jwt-9");
    expect(seenCookie).toBeNull();
    expect(seenKey).toBe("key-9");
  });

  for (const code of ["UNAUTHENTICATED", "SESSION_REVOKED"]) {
    test(`clears session cookies on a 401 ${code}`, async () => {
      const upstream = Bun.serve({
        port: 0,
        hostname: "127.0.0.1",
        fetch: () =>
          Response.json(
            {
              type: "https://jobscrape.local/problems/unauthorized",
              title: "Unauthorized",
              status: 401,
              detail: "x",
              code,
            },
            { status: 401, headers: { "content-type": "application/problem+json" } },
          ),
      });
      servers.push(upstream);

      const res = await proxyRequest(
        new Request("http://bff/api/v1/auth/me", {
          headers: { cookie: "jobscrape_session=jwt-1; jobscrape_csrf=csrf-1" },
        }),
        `http://127.0.0.1:${upstream.port}`,
        "/api/v1/auth/me",
      );

      expect(res.status).toBe(401);
      const cookies = res.headers.getSetCookie();
      expect(
        cookies.some((c) => c.startsWith("jobscrape_session=") && c.includes("Max-Age=0")),
      ).toBe(true);
      expect(cookies.some((c) => c.startsWith("jobscrape_csrf=") && c.includes("Max-Age=0"))).toBe(
        true,
      );
    });
  }

  test("keeps session cookies on a 401 INVALID_CREDENTIALS", async () => {
    const upstream = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: () =>
        Response.json(
          {
            type: "https://jobscrape.local/problems/unauthorized",
            title: "Invalid credentials.",
            status: 401,
            detail: "x",
            code: "INVALID_CREDENTIALS",
          },
          { status: 401, headers: { "content-type": "application/problem+json" } },
        ),
    });
    servers.push(upstream);

    const res = await proxyRequest(
      new Request("http://bff/api/v1/auth/change-password", {
        method: "POST",
        headers: { cookie: "jobscrape_session=jwt-1; jobscrape_csrf=csrf-1" },
        body: "{}",
      }),
      `http://127.0.0.1:${upstream.port}`,
      "/api/v1/auth/change-password",
    );

    expect(res.status).toBe(401);
    expect(res.headers.getSetCookie()).toEqual([]);
  });
});
