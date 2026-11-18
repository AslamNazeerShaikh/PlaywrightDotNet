import { afterAll, afterEach, beforeAll, describe, expect, mock, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor, within } from "@testing-library/react";

import {
  type ApiProblem,
  createApiClient,
  type FetchImpl,
  type RunPayload,
  type StartRunInput,
} from "../../client/lib/api.ts";
import type { LogEvent } from "../../client/lib/telemetry.ts";
import {
  pageWindow,
  profileInputs,
  runDuration,
  RunsPage,
  stageTone,
  upsertRun,
} from "../../client/pages/runs.tsx";
import { Meter } from "../../client/pages/sources.tsx";

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
  onlyNew: false,
  pagesFetched: 1,
  postingsFound: 20,
  postingsNew: 20,
  postingsUpdated: 0,
  detailsFetched: 0,
  challenges: 0,
  failures: 0,
  maxPages: 3,
  startPage: 1,
  maxJobs: 60,
  maxDetails: 60,
  pauseReason: null,
  retryAt: null,
  failureReason: null,
  createdAt: "2026-11-02T08:00:00Z",
  startedAt: "2026-11-02T08:00:00Z",
  finishedAt: null,
};

describe("run meters", () => {
  test("a zero run cap reads empty, not full", () => {
    const queries = render(<Meter label="Details" used={0} cap={0} progress />);
    const meter = queries.getByRole("meter", { name: "Details used" });
    expect(meter.textContent).toContain("0 / 0");
    expect(meter.getAttribute("data-level")).toBe("ok");
  });

  test("a zero quota cap still reads blocked", () => {
    const queries = render(<Meter label="Pages" used={0} cap={0} />);
    expect(queries.getByRole("meter", { name: "Pages used" }).getAttribute("data-level")).toBe(
      "full",
    );
  });
});

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
        startPage: 1,
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

  test("pageWindow stays quiet on page one and shows later windows", () => {
    expect(pageWindow(1, 30)).toBe("");
    expect(pageWindow(undefined, 30)).toBe("");
    expect(pageWindow(null, 30)).toBe("");
    expect(pageWindow(31, 30)).toBe(" · 31–60");
    expect(pageWindow(31, 30, " · pages ")).toBe(" · pages 31–60");
  });

  test("profileInputs shows a page window past page one", () => {
    expect(
      profileInputs({
        id: "p1",
        name: "backend",
        sourceKey: "naukri",
        keywords: "c#",
        locations: ["Pune"],
        experienceYears: null,
        postedWithinDays: null,
        remoteOnly: false,
        requiredSkills: [],
        companies: [],
        maxPages: 30,
        startPage: 31,
        maxJobs: 60,
        maxDetails: 0,
        minDelayMs: 2500,
        maxDelayMs: 6000,
        enabled: true,
        createdAt: "2026-11-02T08:00:00Z",
        updatedAt: "2026-11-02T08:00:00Z",
      }),
    ).toBe("Inputs: c# · Pune · any exp · any date · no skill filter · pages 31–60");
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
        startPage: 1,
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
function runsBackend(
  companies: string[] = ["Infosys", "TCS"],
  failExport?: ApiProblem,
  options: {
    expectedBody?: StartRunInput;
    runPatch?: Partial<RunPayload>;
    failCancel?: ApiProblem;
    /** The source's transport flag; omitted means the sources snapshot is unavailable. */
    apiOnly?: boolean;
  } = {},
) {
  const runs: RunPayload[] = [];
  const calls: string[] = [];
  const posted: StartRunInput[] = [];
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
            channel: "Bundled",
            state: "New",
            canLaunch: true,
          },
        ],
        total: 1,
      });
    }
    if (url === "/api/v1/sources") {
      if (options.apiOnly === undefined) {
        return reply(404, { status: 404, title: "Not found", detail: "", code: "BFF_NOT_FOUND" });
      }
      return reply(200, {
        asOf: "2026-11-02T08:00:00Z",
        items: [{ key: "naukri", apiOnly: options.apiOnly }],
      });
    }
    if (url === "/api/v1/runs" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as StartRunInput;
      posted.push(body);
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
        ...options.runPatch,
        companies,
        id: `r${runs.length + 1}`,
        stage: "StartingSession",
        pagesFetched: 0,
      };
      const expectedBody: StartRunInput = options.expectedBody ?? {
        profileId: "p1",
        browserProfileId: "s1",
        enrich: true,
        onlyNew: false,
      };
      expect(body).toEqual(expectedBody);
      runs.unshift(run);
      return reply(202, run);
    }
    if (url.startsWith("/api/v1/runs?")) {
      const query = new URL(url, "http://deck").searchParams;
      const offset = Number(query.get("offset") ?? 0);
      const limit = Number(query.get("limit") ?? 50);
      return reply(200, {
        items: runs.slice(offset, offset + limit),
        total: runs.length,
      });
    }
    const exportMatch = /^\/api\/v1\/runs\/(r\d+)\/postings\/export(\?|$)/.exec(url);
    if (exportMatch) {
      if (failExport !== undefined) {
        return {
          ok: false,
          status: failExport.status,
          json: async () => failExport,
        };
      }
      const query = new URL(url, "http://deck").searchParams;
      const format = query.get("format") ?? "csv";
      return {
        ok: true,
        status: 200,
        json: async () => ({}),
        blob: async () => new Blob(["a,b"], { type: "text/csv" }),
        headers: {
          get: (name: string) =>
            name === "content-disposition"
              ? `attachment; filename="run-postings-${exportMatch[1]}.${format}"`
              : null,
        },
      };
    }
    const cancelMatch = /^\/api\/v1\/runs\/(r\d+)\/cancel$/.exec(url);
    if (cancelMatch && init?.method === "POST") {
      if (options.failCancel !== undefined) {
        return reply(options.failCancel.status, options.failCancel);
      }
      const index = runs.findIndex((r) => r.id === cancelMatch[1]);
      const cancelled: RunPayload = { ...runs[index]!, stage: "Cancelled", isTerminal: true };
      runs[index] = cancelled;
      return reply(200, cancelled);
    }
    const postings = /^\/api\/v1\/runs\/(r\d+)\/postings(\?|$)/.exec(url);
    if (postings) {
      const done = runs.find((r) => r.id === postings[1])?.isTerminal ?? false;
      const items = done
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
        : [];
      return reply(200, { runId: postings[1], items, total: items.length });
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
  return { client: createApiClient(fetchImpl), calls, posted };
}

describe("runs page", () => {
  test("starts a run, follows it to completion, then stops polling", async () => {
    const { client, calls } = runsBackend();
    const events: LogEvent[] = [];
    const queries = render(
      <RunsPage client={client} onEvent={(e) => events.push(e)} onUnauthorized={() => undefined} />,
    );

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
    expect(queries.getByRole("columnheader", { name: "#" })).toBeDefined();
    expect(queries.getByRole("columnheader", { name: "#" }).className).toContain("rank-cell");
    const firstDataRow = queries.getByRole("table", { name: "Postings" }).querySelector("tbody tr");
    expect(queries.getByRole("columnheader", { name: "Posted" }).className).toContain(
      "posted-cell",
    );
    const postedCell = firstDataRow?.children[6];
    expect(postedCell?.className).toContain("posted-cell");
    expect(firstDataRow?.firstElementChild?.className).toContain("rank-cell");
    expect(firstDataRow?.firstElementChild?.textContent).toBe("1");
    const csv = queries.getByRole("button", { name: "Export all as CSV" });
    expect(csv.textContent).toBe("CSV");
    const xlsx = queries.getByRole("button", { name: "Export all as XLSX" });
    expect(xlsx.textContent).toBe("XLSX");
    expect(queries.getByRole("button", { name: "Export only companies as CSV" })).toBeDefined();
    expect(queries.getByRole("button", { name: "Export exclude companies as XLSX" })).toBeDefined();
    const filterLine = [...queries.container.querySelectorAll("p.status-line")].find((p) =>
      p.textContent?.includes("Company filter (2 companies)"),
    );
    expect(filterLine).toBeDefined();
    expect(calls).toContain("GET /api/v1/runs?offset=0&limit=50");
    expect(calls).toContain("GET /api/v1/runs/r1/postings?offset=0&limit=200");
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
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(
      () =>
        expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
          "Senior .NET Developer",
        ),
      { timeout: 6000 },
    );

    expect(queries.getByRole("button", { name: "Export all as CSV" })).toBeDefined();
    expect(queries.queryByRole("button", { name: "Export only companies as CSV" })).toBeNull();
    expect(queries.queryByRole("button", { name: "Export exclude companies as CSV" })).toBeNull();
  }, 15_000);

  test("export buttons download the blob with the server filename", async () => {
    const { client } = runsBackend();
    const events: LogEvent[] = [];
    const queries = render(
      <RunsPage client={client} onEvent={(e) => events.push(e)} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(
      () =>
        expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
          "Senior .NET Developer",
        ),
      { timeout: 6000 },
    );

    const created: string[] = [];
    const revoked: string[] = [];
    const clicked: Array<{ href: string; download: string }> = [];
    const origCreate = URL.createObjectURL;
    const origRevoke = URL.revokeObjectURL;
    const origClick = HTMLAnchorElement.prototype.click;
    URL.createObjectURL = ((blob: Blob) => {
      expect(blob).toBeInstanceOf(Blob);
      created.push("blob:mock");
      return "blob:mock";
    }) as typeof URL.createObjectURL;
    URL.revokeObjectURL = ((url: string) => {
      revoked.push(url);
    }) as typeof URL.revokeObjectURL;
    HTMLAnchorElement.prototype.click = function (this: HTMLAnchorElement) {
      clicked.push({
        href: this.getAttribute("href") ?? "",
        download: this.getAttribute("download") ?? "",
      });
    };
    try {
      fireEvent.click(queries.getByRole("button", { name: "Export only companies as XLSX" }));
      await waitFor(() => expect(events.map((e) => e.code)).toContain("EXPORTED"), {
        timeout: 6000,
      });
    } finally {
      URL.createObjectURL = origCreate;
      URL.revokeObjectURL = origRevoke;
      HTMLAnchorElement.prototype.click = origClick;
    }
    expect(created).toEqual(["blob:mock"]);
    expect(clicked).toEqual([{ href: "blob:mock", download: "run-postings-r1.xlsx" }]);
    expect(revoked).toEqual(["blob:mock"]);
  }, 15_000);

  test("a failed export shows the problem code", async () => {
    const { client } = runsBackend(["Infosys", "TCS"], {
      status: 404,
      title: "Run not found.",
      detail: "Run 'r1' does not exist.",
      code: "ROW_NOT_FOUND",
    });
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(
      () =>
        expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
          "Senior .NET Developer",
        ),
      { timeout: 6000 },
    );

    fireEvent.click(queries.getByRole("button", { name: "Export all as CSV" }));
    await waitFor(() => expect(queries.getByRole("alert").textContent).toContain("ROW_NOT_FOUND"));
  }, 15_000);

  test("a failed cancel shows the problem code", async () => {
    const { client } = runsBackend(["Infosys", "TCS"], undefined, {
      failCancel: {
        status: 409,
        title: "The run already finished.",
        detail: "",
        code: "RUN_NOT_CANCELLABLE",
      },
    });
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    const cancel = await waitFor(() => queries.getByRole("button", { name: "CANCEL RUN" }));
    fireEvent.click(cancel);

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("RUN_NOT_CANCELLABLE"),
    );
  });

  test("a second start while one executes shows RUN_IN_PROGRESS", async () => {
    const { client } = runsBackend();
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
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

  test("renders the NEW ONLY checkbox unchecked by default", async () => {
    const { client } = runsBackend();
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());
    const checkbox = queries.getByRole("checkbox", { name: "New only" }) as HTMLInputElement;
    expect(checkbox.checked).toBe(false);
  });

  test("sends onlyNew true when checked", async () => {
    const { client, posted } = runsBackend(["Infosys", "TCS"], undefined, {
      expectedBody: { profileId: "p1", browserProfileId: "s1", enrich: true, onlyNew: true },
    });
    const events: LogEvent[] = [];
    const queries = render(
      <RunsPage client={client} onEvent={(e) => events.push(e)} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.click(queries.getByRole("checkbox", { name: "New only" }));
    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() => expect(events.map((e) => e.code)).toContain("RUN_STARTED"));
    expect(posted).toEqual([
      { profileId: "p1", browserProfileId: "s1", enrich: true, onlyNew: true },
    ]);
  });

  test("badges a flagged run and notes filtered postings", async () => {
    const { client } = runsBackend(["Infosys", "TCS"], undefined, {
      runPatch: { onlyNew: true },
    });
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() =>
      expect(
        within(queries.getByRole("list", { name: "Run stages" })).getByText("NEW ONLY"),
      ).toBeDefined(),
    );
    expect(queries.getByText(/the list holds postings first seen in this run/)).toBeDefined();
  });

  test("an API-only source drops the session step and sends no browser profile", async () => {
    const { client, posted } = runsBackend(["Infosys", "TCS"], undefined, {
      apiOnly: true,
      expectedBody: { profileId: "p1", enrich: true, onlyNew: false },
    });
    const events: LogEvent[] = [];
    const queries = render(
      <RunsPage client={client} onEvent={(e) => events.push(e)} onUnauthorized={() => undefined} />,
    );
    await waitFor(() =>
      expect(queries.getByRole("option", { name: /backend · naukri/ })).toBeDefined(),
    );

    // The session step does not exist in API mode, so nothing to disable or explain away.
    expect(queries.queryByRole("combobox", { name: "Session" })).toBeNull();
    expect(queries.queryByText(/Create a naukri session/)).toBeNull();
    expect(queries.getByText("API", { selector: ".code-tag" })).toBeDefined();
    expect(queries.getByText(/no browser session is needed/, { selector: "p" })).toBeDefined();

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() => expect(events.map((e) => e.code)).toContain("RUN_STARTED"));
    expect(posted).toEqual([{ profileId: "p1", enrich: true, onlyNew: false }]);
    expect("browserProfileId" in (posted[0] ?? {})).toBe(false);
  });

  test("a browser source keeps the session step and sends its session", async () => {
    const { client, posted } = runsBackend(["Infosys", "TCS"], undefined, { apiOnly: false });
    const events: LogEvent[] = [];
    const queries = render(
      <RunsPage client={client} onEvent={(e) => events.push(e)} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    expect(queries.getByRole("combobox", { name: "Session" })).toBeDefined();
    expect(queries.queryByText(/no browser session is needed/, { selector: "p" })).toBeNull();

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() => expect(events.map((e) => e.code)).toContain("RUN_STARTED"));
    expect(posted).toEqual([
      { profileId: "p1", browserProfileId: "s1", enrich: true, onlyNew: false },
    ]);
  });
});

/** A failed timed call for overriding one client method in a test. */
function failedCall(problem: ApiProblem) {
  return {
    result: { ok: false as const, problem },
    ms: 1,
    at: "2026-11-02T08:00:01Z",
  };
}

describe("runs page postings failures", () => {
  test("a thrown postings fetch shows a problem and keeps polling", async () => {
    const { client: base } = runsBackend();
    let postingsCalls = 0;
    const client = {
      ...base,
      listRunPostings: () => {
        postingsCalls += 1;
        throw new Error("boom");
      },
    };
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() => expect(queries.getByRole("alert").textContent).toContain("BAD_RESPONSE"));
    expect(queries.getByRole("alert").textContent).not.toContain("boom");
    expect(queries.queryByText("Loading postings…")).toBeNull();
    // The loop survives the throw: the next poll runs on its own.
    await waitFor(() => expect(postingsCalls).toBeGreaterThan(1), { timeout: 6000 });
  }, 15_000);

  test("RETRY refetches the postings and clears the problem on success", async () => {
    const unavailable: ApiProblem = {
      status: 503,
      title: "Postings are unavailable.",
      detail: "",
      code: "BAD_RESPONSE",
    };
    const { client: base } = runsBackend(["Infosys", "TCS"], undefined, {
      runPatch: { isTerminal: true },
    });
    let failuresLeft = 1;
    const client = {
      ...base,
      listRunPostings: (id: string, offset?: number, limit?: number) => {
        if (failuresLeft > 0) {
          failuresLeft -= 1;
          return Promise.resolve(failedCall(unavailable));
        }
        return base.listRunPostings(id, offset, limit);
      },
    };
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(() => expect(queries.getByRole("alert").textContent).toContain("BAD_RESPONSE"));

    fireEvent.click(queries.getByRole("button", { name: "RETRY" }));

    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
        "Senior .NET Developer",
      ),
    );
    expect(queries.queryByRole("alert")).toBeNull();
  }, 15_000);

  test("a terminal run with a failing postings list shows the error, not the spinner", async () => {
    const unavailable: ApiProblem = {
      status: 503,
      title: "Postings are unavailable.",
      detail: "",
      code: "BAD_RESPONSE",
    };
    const { client: base } = runsBackend(["Infosys", "TCS"], undefined, {
      runPatch: { isTerminal: true },
    });
    let postingsCalls = 0;
    const client = {
      ...base,
      listRunPostings: () => {
        postingsCalls += 1;
        return Promise.resolve(failedCall(unavailable));
      },
    };
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() => expect(queries.getByRole("alert").textContent).toContain("BAD_RESPONSE"));
    expect(queries.getByRole("alert").textContent).toContain("Postings are unavailable.");
    expect(queries.queryByText("Loading postings…")).toBeNull();

    // Terminal: the failed poll is the last one.
    await new Promise((resolve) => setTimeout(resolve, 2500));
    expect(postingsCalls).toBe(1);
  }, 15_000);

  test("session death on the follow poll still routes out", async () => {
    const unauthorized: ApiProblem = {
      status: 401,
      title: "Your session ended. Sign in again to pick up any account changes.",
      detail: "",
      code: "UNAUTHENTICATED",
    };
    const onUnauthorized = mock((_problem: ApiProblem) => undefined);
    const { client: base } = runsBackend();
    const client = {
      ...base,
      getRun: () => Promise.resolve(failedCall(unauthorized)),
      listRunPostings: () => Promise.resolve(failedCall(unauthorized)),
    };
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={onUnauthorized} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledTimes(1));
    expect(onUnauthorized.mock.calls[0]?.[0]).toEqual(unauthorized);
    // The shell owns the sign-in route; the page never shows a postings error for it.
    expect(queries.queryByRole("alert")).toBeNull();
    expect(queries.queryByRole("button", { name: "RETRY" })).toBeNull();
  }, 15_000);

  test("re-selecting the active run refetches its postings", async () => {
    const { client, calls } = runsBackend();
    const queries = render(
      <RunsPage client={client} onEvent={() => undefined} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("option", { name: /naukri-main/ })).toBeDefined());

    fireEvent.submit(queries.getByRole("form", { name: "Start a run" }));
    await waitFor(
      () =>
        expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
          "Senior .NET Developer",
        ),
      { timeout: 6000 },
    );

    const postingsCalls = () =>
      calls.filter((call) => call.startsWith("GET /api/v1/runs/r1/postings?")).length;
    const before = postingsCalls();
    fireEvent.click(queries.getByRole("button", { name: "2026-11-02 08:00:00" }));

    await waitFor(() => expect(postingsCalls()).toBeGreaterThan(before));
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Postings" }).textContent).toContain(
        "Senior .NET Developer",
      ),
    );
  }, 15_000);
});
