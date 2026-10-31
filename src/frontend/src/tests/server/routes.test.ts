import { describe, expect, test } from "bun:test";

import { createHandler } from "../../server/index.ts";

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
