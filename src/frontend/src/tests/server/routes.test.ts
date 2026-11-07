import { afterEach, describe, expect, test } from "bun:test";

import { createHandler } from "../../server/index.ts";

let upstreams: Array<{ stop(): void }> = [];

afterEach(() => {
  for (const upstream of upstreams) {
    upstream.stop();
  }
  upstreams = [];
});

function hitCountingUpstream(): { url: string; hits: () => number } {
  let hits = 0;
  const server = Bun.serve({
    port: 0,
    hostname: "127.0.0.1",
    fetch: () => {
      hits += 1;
      return Response.json({ ok: true });
    },
  });
  upstreams.push(server);
  return { url: `http://127.0.0.1:${server.port}`, hits: () => hits };
}

const csrfHeaders = {
  origin: "http://bff",
  "sec-fetch-site": "same-origin",
  cookie: "jobscrape_csrf=csrf-1",
  "x-csrf-token": "csrf-1",
};

describe("BFF routes", () => {
  const handler = createHandler({
    port: 3000,
    dotnetApiUrl: "http://127.0.0.1:1",
  });

  test("healthz reports ok without touching the data plane", async () => {
    const res = await handler.fetch(new Request("http://bff/healthz"));

    expect(res.status).toBe(200);
    expect(await res.json()).toEqual({
      status: "ok",
      upstream: "http://127.0.0.1:1",
    });
  });

  test("unmatched paths answer 404 problem+json with a stable code", async () => {
    const res = await handler.fetch(new Request("http://bff/nope"));

    expect(res.status).toBe(404);
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("BFF_NOT_FOUND");
  });

  test("/api paths proxy upstream (502 here: nothing listens on port 1)", async () => {
    const res = await handler.fetch(new Request("http://bff/api/v1/health"));

    expect(res.status).toBe(502);
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("DOTNET_UNREACHABLE");
  });
});

describe("blocked direct auth endpoints", () => {
  for (const variant of ["/api/v1/Auth/Login", "/api/v1/auth/%6cogin", "/api/v1/auth/login/"]) {
    test(`POST ${variant} answers 404 without hitting upstream`, async () => {
      const upstream = hitCountingUpstream();
      const localHandler = createHandler({ port: 3000, dotnetApiUrl: upstream.url });

      const res = await localHandler.fetch(
        new Request(`http://bff${variant}`, {
          method: "POST",
          headers: { "content-type": "application/json", ...csrfHeaders },
          body: JSON.stringify({ email: "a@x.com", password: "s3cret" }),
        }),
      );

      expect(res.status).toBe(404);
      const body = (await res.json()) as { code: string };
      expect(body.code).toBe("BFF_NOT_FOUND");
      expect(upstream.hits()).toBe(0);
    });
  }
});

describe("proxy upstream 401", () => {
  const changePasswordHeaders = {
    origin: "http://bff",
    "sec-fetch-site": "same-origin",
    cookie: "jobscrape_session=jwt-9; jobscrape_csrf=csrf-9",
    "x-csrf-token": "csrf-9",
    "content-type": "application/json",
  };
  const postChangePassword = (localHandler: ReturnType<typeof createHandler>) =>
    localHandler.fetch(
      new Request("http://bff/api/v1/auth/change-password", {
        method: "POST",
        headers: changePasswordHeaders,
        body: JSON.stringify({
          currentPassword: "wrong-password",
          newPassword: "n3w-password!",
        }),
      }),
    );

  const problemUpstream = (status: number, code: string) => {
    const server = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: () =>
        Response.json(
          {
            type: `https://jobscrape.local/problems/${code.toLowerCase().replace(/_/g, "-")}`,
            title: "Unauthorized",
            status,
            detail: "Detail.",
            code,
          },
          { status, headers: { "content-type": "application/problem+json" } },
        ),
    });
    upstreams.push(server);
    return `http://127.0.0.1:${server.port}`;
  };

  test("401 INVALID_CREDENTIALS keeps the session so the operator can retry", async () => {
    let calls = 0;
    const upstream = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: () => {
        calls += 1;
        if (calls === 1) {
          return Response.json(
            {
              type: "https://jobscrape.local/problems/invalid-credentials",
              title: "Invalid email or password.",
              status: 401,
              detail: "The email or password is incorrect.",
              code: "INVALID_CREDENTIALS",
            },
            { status: 401, headers: { "content-type": "application/problem+json" } },
          );
        }
        return new Response(null, { status: 204 });
      },
    });
    upstreams.push(upstream);
    const localHandler = createHandler({
      port: 3000,
      dotnetApiUrl: `http://127.0.0.1:${upstream.port}`,
    });

    const first = await postChangePassword(localHandler);

    expect(first.status).toBe(401);
    expect(first.headers.getSetCookie()).toEqual([]);
    const body = (await first.json()) as { code: string };
    expect(body.code).toBe("INVALID_CREDENTIALS");

    const retry = await postChangePassword(localHandler);

    expect(retry.status).toBe(204);
  });

  test("401 UNAUTHENTICATED clears session and CSRF cookies with Max-Age=0", async () => {
    const localHandler = createHandler({
      port: 3000,
      dotnetApiUrl: problemUpstream(401, "UNAUTHENTICATED"),
    });

    const res = await postChangePassword(localHandler);

    expect(res.status).toBe(401);
    expect(res.headers.get("content-type")).toContain("application/problem+json");
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("UNAUTHENTICATED");
    const cookies = res.headers.getSetCookie();
    expect(cookies).toHaveLength(3);
    for (const cookie of cookies) {
      expect(cookie).toContain("Max-Age=0");
    }
    expect(cookies.some((c) => c.startsWith("jobscrape_session="))).toBe(true);
    expect(cookies.some((c) => c.startsWith("__Host-jobscrape_session="))).toBe(true);
    expect(cookies.some((c) => c.startsWith("jobscrape_csrf="))).toBe(true);
  });
});

describe("session route guards", () => {
  const handler = createHandler({ port: 3000, dotnetApiUrl: "http://127.0.0.1:1" });
  const postSession = (headers: Record<string, string>) =>
    handler.fetch(new Request("http://bff/bff/session", { method: "POST", headers, body: "{}" }));

  test("POST without Origin answers 403", async () => {
    const res = await postSession({ "sec-fetch-site": "same-origin" });

    expect(res.status).toBe(403);
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("ORIGIN_NOT_ALLOWED");
  });

  test("POST with a foreign Origin answers 403", async () => {
    const res = await postSession({
      origin: "https://evil.example",
      "sec-fetch-site": "same-origin",
    });

    expect(res.status).toBe(403);
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("ORIGIN_NOT_ALLOWED");
  });

  test("DELETE with a foreign Origin answers 403", async () => {
    const res = await handler.fetch(
      new Request("http://bff/bff/session", {
        method: "DELETE",
        headers: { ...csrfHeaders, origin: "https://evil.example" },
      }),
    );

    expect(res.status).toBe(403);
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("ORIGIN_NOT_ALLOWED");
  });

  test("DELETE without the CSRF pair answers 403", async () => {
    const res = await handler.fetch(
      new Request("http://bff/bff/session", {
        method: "DELETE",
        headers: { origin: "http://bff", "sec-fetch-site": "same-origin" },
      }),
    );

    expect(res.status).toBe(403);
    const body = (await res.json()) as { code: string };
    expect(body.code).toBe("CSRF_FAILED");
  });

  const upstreamJson = (payload: unknown) => {
    const server = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: () => Response.json(payload),
    });
    upstreams.push(server);
    return `http://127.0.0.1:${server.port}`;
  };

  const loginCookies = async (scheme: "http" | "https") => {
    const dotnetApiUrl = upstreamJson({ token: "tok-abc", mustChangePassword: false });
    const localHandler = createHandler({ port: 3000, dotnetApiUrl });
    const url = `${scheme}://bff/bff/session`;
    const res = await localHandler.fetch(
      new Request(url, {
        method: "POST",
        headers: {
          "content-type": "application/json",
          origin: `${scheme}://bff`,
          "sec-fetch-site": "same-origin",
        },
        body: JSON.stringify({ email: "a@x.com", password: "s3cret" }),
      }),
    );
    expect(res.status).toBe(200);
    return res.headers.getSetCookie();
  };

  test("login over http sets non-secure cookies without __Host-", async () => {
    const cookies = await loginCookies("http");
    const session = cookies.find((c) => c.startsWith("jobscrape_session="));
    const csrf = cookies.find((c) => c.startsWith("jobscrape_csrf="));

    expect(session).toBeDefined();
    expect(csrf).toBeDefined();
    expect(cookies.some((c) => c.startsWith("__Host-"))).toBe(false);
    for (const cookie of [session as string, csrf as string]) {
      expect(cookie).toContain("Path=/");
      expect(cookie).toContain("SameSite=Strict");
    }
    expect(session).toContain("HttpOnly");
    expect(session).not.toContain("Secure");
  });

  test("login over https sets __Host- session with Secure", async () => {
    const cookies = await loginCookies("https");
    const session = cookies.find((c) => c.startsWith("__Host-jobscrape_session="));
    const csrf = cookies.find((c) => c.startsWith("jobscrape_csrf="));

    expect(session).toBeDefined();
    expect(csrf).toBeDefined();
    expect(cookies.some((c) => c.startsWith("jobscrape_session="))).toBe(false);
    for (const cookie of [session as string, csrf as string]) {
      expect(cookie).toContain("Path=/");
      expect(cookie).toContain("SameSite=Strict");
      expect(cookie).toContain("Secure");
    }
    expect(session).toContain("HttpOnly");
  });
});
