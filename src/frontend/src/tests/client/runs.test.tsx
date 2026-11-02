import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import {
  createApiClient,
  type FetchImpl,
  type RunPayload,
  type StartRunInput,
} from "../../client/lib/api.ts";
import type { LogEvent } from "../../client/lib/telemetry.ts";
import {
  profileInputs,
  runDuration,
  RunsPage,
  stageTone,
  upsertRun,
} from "../../client/pages/runs.tsx";

beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
});

const baseRun: RunPayload = {
  id: "r1",
  profileId: "p1",
  profileName: "backend",
  companies: ["Infosys", "TCS"],
  sourceKey: "naukri",
  stage: "Searching",
  isTerminal: false,
  pagesFetched: 1,
  postingsFound: 20,
  postingsNew: 20,
  postingsUpdated: 0,
  detailsFetched: 0,
  challenges: 0,
  failures: 0,
  maxPages: 3,
  maxJobs: 60,
  maxDetails: 60,
  pauseReason: null,
  retryAt: null,
  failureReason: null,
  createdAt: "2026-11-02T08:00:00Z",
  startedAt: "2026-11-02T08:00:00Z",
  finishedAt: null,
};

describe("run helpers", () => {
  test("stage tones: finished well, badly, waiting, or moving", () => {
    expect(stageTone("Completed")).toBe("ok");
    expect(stageTone("Failed")).toBe("bad");
    expect(stageTone("Cancelled")).toBe("warn");
    expect(stageTone("PausedForChallenge")).toBe("warn");
    expect(stageTone("RateLimited")).toBe("warn");
    expect(stageTone("Searching")).toBeUndefined();
  });

  test("upsertRun replaces by id and puts new runs first", () => {
    const done = { ...baseRun, stage: "Completed", isTerminal: true };
    expect(upsertRun([baseRun], done)).toEqual([done]);
    const other = { ...baseRun, id: "r2" };
    expect(upsertRun([baseRun], other).map((r) => r.id)).toEqual(["r2", "r1"]);
    expect(upsertRun(null, baseRun)).toEqual([baseRun]);
  });

  test("runDuration reads seconds, then minutes; a dash before start", () => {
    const start = Date.parse("2026-11-02T08:00:00Z");
    expect(runDuration(baseRun, start + 42_000)).toBe("42 s");
    expect(runDuration({ ...baseRun, finishedAt: "2026-11-02T08:03:05Z" }, 0)).toBe("3 m 05 s");
    expect(runDuration({ ...baseRun, startedAt: null }, start)).toBe("—");
  });

  test("profileInputs names every search input on one line", () => {
    expect(
      profileInputs({
        id: "p1",
        name: "backend",
        sourceKey: "naukri",
        keywords: "asp.net core c#",
        locations: ["Pune", "Hyderabad"],
        experienceYears: 6,
        postedWithinDays: 1,
        remoteOnly: false,
        requiredSkills: ["C#", "ASP.NET Core"],
        companies: [],
        maxPages: 3,
        maxJobs: 60,
        maxDetails: 60,
        minDelayMs: 2500,
        maxDelayMs: 6000,
        enabled: true,
        createdAt: "2026-11-02T08:00:00Z",
        updatedAt: "2026-11-02T08:00:00Z",
      }),
    ).toBe(
      "Inputs: asp.net core c# · Pune, Hyderabad · 6 yrs · last 1d · skills: C#, ASP.NET Core",
    );
  });

  test("profileInputs falls back when filters are absent", () => {
    expect(
      profileInputs({
        id: "p1",
        name: "remote",
        sourceKey: "naukri",
        keywords: "c#",
        locations: [],
        experienceYears: null,
        postedWithinDays: null,
        remoteOnly: true,
        requiredSkills: [],
        companies: [],
        maxPages: 3,
        maxJobs: 60,
        maxDetails: 60,
        minDelayMs: 2500,
        maxDelayMs: 6000,
        enabled: true,
        createdAt: "2026-11-02T08:00:00Z",
        updatedAt: "2026-11-02T08:00:00Z",
      }),
    ).toBe("Inputs: c# · remote · any exp · any date · no skill filter");
  });
});

/**
 * A stateful fake of the runs API: POST starts a run that advances one stage per poll and completes
 * with two postings; a second POST while it executes answers RUN_IN_PROGRESS.
 */
function runsBackend(companies: string[] = ["Infosys", "TCS"]) {
  const runs: RunPayload[] = [];
  const calls: string[] = [];
  let polls = 0;
  const fetchImpl: FetchImpl = async (url: string, init?: RequestInit) => {
    calls.push(`${init?.method ?? "GET"} ${url}`);
    const reply = (status: number, body: unknown) => ({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    });
    if (url === "/api/v1/search-profiles") {
      return reply(200, {
        items: [
          {
            id: "p1",
            name: "backend",
            sourceKey: "naukri",
            keywords: "asp.net core c#",
            locations: ["Pune", "Hyderabad"],
            experienceYears: 6,
            postedWithinDays: 1,
            remoteOnly: false,
            requiredSkills: ["C#", "ASP.NET Core"],
            companies: ["Infosys", "TCS"],
            enabled: true,
          },
        ],
        total: 1,
      });
    }
    if (url === "/api/v1/sessions") {
      return reply(200, {
        items: [
          {
            id: "s1",
            name: "naukri-main",
            sourceKey: "naukri",
            engine: "Chromium",
            channel: "Chrome",
            state: "New",
            canLaunch: true,
          },
        ],
        total: 1,
      });
    }
    if (url === "/api/v1/runs" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as StartRunInput;
      if (runs.some((r) => !r.isTerminal)) {
        return reply(409, {
          status: 409,
          title: "A run is already in progress.",
          detail: "",
          code: "RUN_IN_PROGRESS",
        });
      }
      const run = {
        ...baseRun,
        companies,
        id: `r${runs.length + 1}`,
        stage: "StartingSession",
        pagesFetched: 0,
      };
      expect(body).toEqual({ profileId: "p1", browserProfileId: "s1", enrich: true });
      runs.unshift(run);
      return reply(202, run);
    }
    if (url === "/api/v1/runs") {
      return reply(200, { items: runs, total: runs.length });
    }
    const postings = /^\/api\/v1\/runs\/(r\d+)\/postings$/.exec(url);
    if (postings) {
      const done = runs.find((r) => r.id === postings[1])?.isTerminal ?? false;
      return reply(200, {
        runId: postings[1],
        items: done
          ? [
              {
                id: "j1",
                externalJobId: "101",
                url: "https://www.naukri.com/job-listings-dotnet-dev-101",
                title: "Senior .NET Developer",
                company: "Contoso",
                location: "Pune",
                workMode: "Onsite",
                experience: "5-8 Yrs",
                salary: "15-25 Lacs PA",
                postedAt: "2026-11-01T00:00:00Z",
                skills: ["C#", "ASP.NET Core"],
                enriched: true,
                isNew: true,
                firstSeenAt: "2026-11-02T08:00:10Z",
                lastSeenAt: "2026-11-02T08:00:10Z",
              },
            ]
          : [],
      });
    }
    const one = /^\/api\/v1\/runs\/(r\d+)$/.exec(url);
    if (one) {
      polls += 1;
      const index = runs.findIndex((r) => r.id === one[1]);
      const run = runs[index]!;
      runs[index] =
        polls >= 2
          ? {
              ...run,
              stage: "Completed",
              isTerminal: true,
              pagesFetched: 1,
              maxPages: 1,
              postingsFound: 1,
              postingsNew: 1,
              finishedAt: "2026-11-02T08:00:42Z",
            }
          : { ...run, stage: "Searching" };
      return reply(200, runs[index]);
    }
    return reply(404, { status: 404, title: "Not found", detail: "", code: "BFF_NOT_FOUND" });
  };
  return { client: createApiClient(fetchImpl), calls };
}

describe("runs page", () => {
  test("starts a run, follows it to completion, then stops polling", async () => {
    const { client, calls } = runsBackend();
    const events: LogEvent[] = [];
    const queries = render(<RunsPage client={client} onEvent={(e) => events.push(e)} />);

    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());
    expect(queries.getByText("No runs yet. Start one to see it here.")).toBeDefined();

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(() => expect(events.map((e) => e.code)).toContain("RUN_STARTED"));

    // Polls every 2 s until terminal: wait for the completion event.
    await waitFor(() => expect(events.map((e) => e.code)).toContain("COMPLETED"), {
      timeout: 6000,
    });
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
        "Senior .NET Developer",
      ),
    );
    const csv = queries.getByRole("link", { name: "Export all as CSV" });
    expect(csv.getAttribute("href")).toBe("/api/v1/runs/r1/postings/export?format=csv");
    const xlsx = queries.getByRole("link", { name: "Export all as XLSX" });
    expect(xlsx.getAttribute("href")).toBe("/api/v1/runs/r1/postings/export?format=xlsx");
    const onlyCsv = queries.getByRole("link", { name: "Export only companies as CSV" });
    expect(onlyCsv.getAttribute("href")).toBe(
      "/api/v1/runs/r1/postings/export?format=csv&group=only",
    );
    const excludeXlsx = queries.getByRole("link", {
      name: "Export exclude companies as XLSX",
    });
    expect(excludeXlsx.getAttribute("href")).toBe(
      "/api/v1/runs/r1/postings/export?format=xlsx&group=exclude",
    );
    expect(queries.getByRole("status").textContent).toContain("COMPLETED");
    expect(queries.queryByRole("button", { name: "CANCEL RUN" })).toBeNull();
    // Run meters measure progress: a cap reached is done, never the quota red.
    expect(queries.getByRole("meter", { name: "Pages used" }).getAttribute("data-level")).toBe(
      "done",
    );
    expect(
      queries.getByRole("meter", { name: "Postings used" }).getAttribute("aria-valuenow"),
    ).toBe("1");

    // Terminal: no further run polls.
    const settled = calls.filter((c) => c === "GET /api/v1/runs/r1").length;
    await new Promise((resolve) => setTimeout(resolve, 2500));
    expect(calls.filter((c) => c === "GET /api/v1/runs/r1").length).toBe(settled);
  }, 15_000);

  test("hides company split links when the run has no company list", async () => {
    const { client } = runsBackend([]);
    const queries = render(<RunsPage client={client} onEvent={() => undefined} />);
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(
      () =>
        expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
          "Senior .NET Developer",
        ),
      { timeout: 6000 },
    );

    expect(queries.getByRole("link", { name: "Export all as CSV" })).toBeDefined();
    expect(queries.queryByRole("link", { name: "Export only companies as CSV" })).toBeNull();
    expect(queries.queryByRole("link", { name: "Export exclude companies as CSV" })).toBeNull();
  }, 15_000);

  test("a second start while one executes shows RUN_IN_PROGRESS", async () => {
    const { client } = runsBackend();
    const queries = render(<RunsPage client={client} onEvent={() => undefined} />);
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    expect(queries.getByText(/^Inputs: /).textContent).toContain(
      "Inputs: asp.net core c# · Pune, Hyderabad · 6 yrs · last 1d · skills: C#, ASP.NET Core",
    );

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(() => expect(queries.getByRole("button", { name: "CANCEL RUN" })).toBeDefined());
    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("RUN_IN_PROGRESS"),
    );
  });
});
