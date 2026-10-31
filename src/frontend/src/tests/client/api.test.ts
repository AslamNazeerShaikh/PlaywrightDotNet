import { describe, expect, test } from "bun:test";

import { createApiClient, type FetchImpl } from "../../client/lib/api.ts";

function stubJson(status: number, body: unknown): FetchImpl {
  return async () => ({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  });
}

describe("api client", () => {
  test("fetchHealth returns the timed payload", async () => {
    const client = createApiClient(
      stubJson(200, { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" }),
    );

    const { result, ms, at } = await client.fetchHealth();

    expect(result).toEqual({
      ok: true,
      value: { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" },
    });
    expect(ms).toBeGreaterThanOrEqual(0);
    expect(Number.isNaN(Date.parse(at))).toBe(false);
  });

  test("fetchMeta returns the BFF self-check", async () => {
    const byPath: FetchImpl = async (input: string) =>
      input === "/healthz"
        ? { ok: true, status: 200, json: async () => ({ status: "ok", upstream: "http://x" }) }
        : { ok: false, status: 404, json: async () => ({}) };
    const client = createApiClient(byPath);

    const { result } = await client.fetchMeta();

    expect(result).toEqual({ ok: true, value: { status: "ok", upstream: "http://x" } });
  });

  test("fetchProfile returns the timed profile on success", async () => {
    const client = createApiClient(
      stubJson(200, { id: "abc", name: "Backend", sourceKey: "naukri" }),
    );

    const { result } = await client.fetchProfile("abc");

    expect(result.ok).toBe(true);
  });

  test("failures surface the stable problem code", async () => {
    const client = createApiClient(
      stubJson(404, {
        status: 404,
        title: "Search profile not found.",
        detail: "Search profile 'abc' does not exist.",
        code: "PROFILE_NOT_FOUND",
      }),
    );

    const { result } = await client.fetchProfile("abc");

    expect(result).toEqual({
      ok: false,
      problem: {
        status: 404,
        title: "Search profile not found.",
        detail: "Search profile 'abc' does not exist.",
        code: "PROFILE_NOT_FOUND",
      },
    });
  });

  test("network failure maps to BFF_UNREACHABLE", async () => {
    const refusing: FetchImpl = async () => {
      throw new Error("refused");
    };
    const client = createApiClient(refusing);

    const { result } = await client.fetchHealth();

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.code).toBe("BFF_UNREACHABLE");
    }
  });

  test("non-json or code-less bodies fall back safely", async () => {
    const broken: FetchImpl = async () => ({
      ok: false,
      status: 500,
      json: async () => {
        throw new Error("not json");
      },
    });
    const codeless = createApiClient(stubJson(500, { unexpected: true }));

    const brokenResult = await createApiClient(broken).fetchHealth();
    const codelessResult = await codeless.fetchHealth();

    expect(brokenResult.result.ok).toBe(false);
    if (!brokenResult.result.ok) {
      expect(brokenResult.result.problem.code).toBe("UNKNOWN");
    }
    expect(codelessResult.result.ok).toBe(false);
    if (!codelessResult.result.ok) {
      expect(codelessResult.result.problem.code).toBe("UNKNOWN");
    }
  });
});
