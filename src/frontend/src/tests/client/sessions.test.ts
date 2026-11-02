import { describe, expect, test } from "bun:test";

import { createApiClient, type FetchImpl } from "../../client/lib/api.ts";
import { readSessionUpdateForm, stateLabel, stateTone } from "../../client/pages/sessions.tsx";

describe("sessions", () => {
  test("listSessions GETs and createSession POSTs JSON", async () => {
    const calls: Array<{ url: string; init?: RequestInit }> = [];
    const fetchImpl: FetchImpl = async (url, init) => {
      calls.push({ url, init });
      return {
        ok: true,
        status: url === "/api/v1/sessions" && init ? 201 : 200,
        json: async () => ({ items: [], total: 0 }),
      };
    };
    const client = createApiClient(fetchImpl);

    await client.listSessions();
    await client.createSession({
      name: "main",
      sourceKey: "naukri",
      engine: "Chromium",
      channel: "Bundled",
    });

    expect(calls[0]).toEqual({ url: "/api/v1/sessions", init: undefined });
    expect(calls[1]?.init?.method).toBe("POST");
    expect(JSON.parse(String(calls[1]?.init?.body))).toEqual({
      name: "main",
      sourceKey: "naukri",
      engine: "Chromium",
      channel: "Bundled",
    });
  });

  test("updateSession PATCHes JSON to the session id", async () => {
    let seen: RequestInit | undefined;
    const fetchImpl: FetchImpl = async (_url, init) => {
      seen = init;
      return {
        ok: true,
        status: 200,
        json: async () => ({
          id: "s1",
          name: "after",
          sourceKey: "naukri",
          engine: "Chromium",
          channel: "Bundled",
          state: "New",
          userDataDirectory: "naukri/after",
          canLaunch: true,
          cooldownUntil: null,
          consecutiveChallenges: 0,
          lastUsedAt: null,
          lastVerifiedAt: null,
        }),
      };
    };

    const { result } = await createApiClient(fetchImpl).updateSession("s1", {
      name: "after",
      engine: "Chromium",
      channel: "Bundled",
    });

    expect(result.ok).toBe(true);
    expect(seen?.method).toBe("PATCH");
    expect(seen?.headers).toEqual({ "content-type": "application/json" });
    expect(JSON.parse(String(seen?.body))).toEqual({
      name: "after",
      engine: "Chromium",
      channel: "Bundled",
    });
  });

  test("readSessionUpdateForm trims every field", () => {
    const data = new FormData();
    data.set("name", "  after ");
    data.set("engine", " Chromium ");
    data.set("channel", " Bundled ");

    expect(readSessionUpdateForm(data)).toEqual({
      name: "after",
      engine: "Chromium",
      channel: "Bundled",
    });
  });

  test("state labels read like stable codes and tones follow severity", () => {
    expect(stateLabel("CoolingDown")).toBe("COOLING_DOWN");
    expect(stateLabel("ResetRequired")).toBe("RESET_REQUIRED");
    expect(stateLabel("New")).toBe("NEW");
    expect(stateTone("Healthy")).toBe("ok");
    expect(stateTone("ResetRequired")).toBe("bad");
    expect(stateTone("New")).toBeUndefined();
    expect(stateTone("CoolingDown")).toBe("warn");
    expect(stateTone("Expired")).toBe("warn");
  });
});
