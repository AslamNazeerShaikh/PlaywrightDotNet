import { describe, expect, test } from "bun:test";

import { plural } from "../../client/components/modules.tsx";
import { createApiClient, type FetchImpl } from "../../client/lib/api.ts";
import { circuitText } from "../../client/pages/sources.tsx";

const closed = {
  state: "Closed",
  openUntil: null,
  canProbe: false,
  consecutiveFailures: 0,
  recentFailures: 0,
} as const;

describe("sources", () => {
  test("listSources GETs the snapshot", async () => {
    let seen = "";
    let seenInit: RequestInit | undefined;
    const fetchImpl: FetchImpl = async (url, init) => {
      seen = url;
      seenInit = init;
      return { ok: true, status: 200, json: async () => ({ items: [], asOf: "x" }) };
    };

    const client = createApiClient(fetchImpl);
    const { result } = await client.listSources();

    expect(seen).toBe("/api/v1/sources");

    expect(result).toEqual({ ok: true, value: { items: [], asOf: "x" } });
  });

  test("circuit text covers every state", () => {
    expect(circuitText(closed)).toBe("closed · no recent walls");
    expect(circuitText({ ...closed, recentFailures: 2 })).toBe("closed · 2 wall(s) in 24 h");
    expect(circuitText({ ...closed, state: "HalfOpen" })).toBe("half-open · one probe run allowed");
    expect(circuitText({ ...closed, state: "Open", openUntil: "2026-11-03T08:00:00Z" })).toBe(
      "open until 2026-11-03T08:00:00Z",
    );
    expect(circuitText({ ...closed, state: "Open", canProbe: true })).toBe(
      "open · cooldown over, probe allowed",
    );
    expect(circuitText({ ...closed, state: "Open" })).toBe("open until unknown");
  });

  test("plural reads naturally for one and many", () => {
    expect(plural(1, "page")).toBe("1 page");
    expect(plural(0, "detail")).toBe("0 details");
    expect(plural(25, "job")).toBe("25 jobs");
  });
});
