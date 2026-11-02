import { describe, expect, test } from "bun:test";

import { createApiClient, type FetchImpl } from "../../client/lib/api.ts";
import { readForm } from "../../client/pages/profiles.tsx";

const profile = {
  id: "p1",
  name: "backend",
  sourceKey: "naukri",
  keywords: "c#",
  locations: ["Pune"],
  experienceYears: 6,
  postedWithinDays: null,
  remoteOnly: false,
  maxPages: 3,
  maxJobs: 60,
  maxDetails: 60,
  minDelayMs: 2500,
  maxDelayMs: 6000,
  enabled: true,
  createdAt: "2026-11-02T08:00:00Z",
  updatedAt: "2026-11-02T08:00:00Z",
};

describe("profiles api", () => {
  test("listProfiles GETs the collection", async () => {
    const calls: Array<{ url: string; init?: RequestInit }> = [];
    const fetchImpl: FetchImpl = async (url, init) => {
      calls.push({ url, init });
      return { ok: true, status: 200, json: async () => ({ items: [profile], total: 1 }) };
    };

    const { result } = await createApiClient(fetchImpl).listProfiles();

    expect(result).toEqual({ ok: true, value: { items: [profile], total: 1 } });
    expect(calls).toEqual([{ url: "/api/v1/search-profiles", init: undefined }]);
  });

  test("createProfile POSTs JSON and returns the created profile", async () => {
    let seen: RequestInit | undefined;
    const fetchImpl: FetchImpl = async (_url, init) => {
      seen = init;
      return { ok: true, status: 201, json: async () => profile };
    };
    const input = {
      name: "backend",
      sourceKey: "naukri",
      keywords: "c#",
      locations: ["Pune"],
      experienceYears: 6,
      postedWithinDays: null,
      remoteOnly: false,
    };

    const { result } = await createApiClient(fetchImpl).createProfile(input);

    expect(result).toEqual({ ok: true, value: profile });
    expect(seen?.method).toBe("POST");
    expect(seen?.headers).toEqual({ "content-type": "application/json" });
    expect(JSON.parse(String(seen?.body))).toEqual(input);
  });

  test("validation problems carry field errors", async () => {
    const fetchImpl: FetchImpl = async () => ({
      ok: false,
      status: 400,
      json: async () => ({
        status: 400,
        title: "One or more validation errors occurred.",
        detail: "The request contains invalid fields.",
        code: "VALIDATION_FAILED",
        errors: { name: ["A name is required."], maxPages: ["Too many."] },
      }),
    });

    const { result } = await createApiClient(fetchImpl).createProfile({
      name: "",
      sourceKey: "naukri",
      keywords: "",
      locations: [],
      experienceYears: null,
      postedWithinDays: null,
      remoteOnly: false,
    });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.code).toBe("VALIDATION_FAILED");
      expect(result.problem.errors).toEqual({
        name: ["A name is required."],
        maxPages: ["Too many."],
      });
    }
  });

  test("malformed errors members are ignored, not trusted", async () => {
    for (const errors of [["x"], { name: "not-an-array" }, { name: [1] }, null]) {
      const fetchImpl: FetchImpl = async () => ({
        ok: false,
        status: 400,
        json: async () => ({ code: "VALIDATION_FAILED", errors }),
      });

      const { result } = await createApiClient(fetchImpl).listProfiles();

      expect(result.ok).toBe(false);
      if (!result.ok) {
        expect(result.problem.errors).toBeUndefined();
      }
    }
  });
});

describe("profile form", () => {
  test("readForm trims text, splits locations, and maps blank numbers to null", () => {
    const data = new FormData();
    data.set("name", "  backend ");
    data.set("sourceKey", "indeed");
    data.set("keywords", " c# ");
    data.set("locations", " Pune, ,Hyderabad ,");
    data.set("experienceYears", "6");
    data.set("postedWithinDays", " ");

    expect(readForm(data)).toEqual({
      name: "backend",
      sourceKey: "indeed",
      keywords: "c#",
      locations: ["Pune", "Hyderabad"],
      experienceYears: 6,
      postedWithinDays: null,
      remoteOnly: false,
    });
  });

  test("readForm reads the remote-only checkbox", () => {
    const data = new FormData();
    data.set("remoteOnly", "on");

    expect(readForm(data).remoteOnly).toBe(true);
    expect(readForm(data).locations).toEqual([]);
  });
});
