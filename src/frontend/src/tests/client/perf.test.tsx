import { afterAll, afterEach, beforeAll, describe, expect, setSystemTime, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { act, cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import { App, PROBE_INTERVAL_MS, shouldProbe } from "../../client/app.tsx";
import {
  type ApiClient,
  createApiClient,
  type FetchImpl,
  type SearchProfilePayload,
} from "../../client/lib/api.ts";
import { renderCount } from "../../client/lib/debug.ts";

// Regression guards for the cockpit's runtime cost: what re-renders on the
// clock tick, how often the deck calls the API, and which response wins
// when requests overlap. Each test pins a defect found with the React
// DevTools profiler.
beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
  setSystemTime();
  window.location.hash = "";
  setVisibility("visible");
});

const health = { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" };

function profile(id: string, name: string): SearchProfilePayload {
  return {
    id,
    name,
    sourceKey: "naukri",
    keywords: "c#",
    locations: ["Pune"],
    experienceYears: null,
    postedWithinDays: null,
    remoteOnly: false,
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
  };
}

/** A client that counts calls per path and answers healthy, with no profiles. */
function countingClient() {
  const calls: Record<string, number> = {};
  const fetchImpl: FetchImpl = async (url: string) => {
    calls[url] = (calls[url] ?? 0) + 1;
    const body =
      url === "/healthz"
        ? { status: "ok", upstream: "x" }
        : url === "/api/v1/health"
          ? health
          : { items: [], total: 0 };
    return { ok: true, status: 200, json: async () => body };
  };
  return { client: createApiClient(fetchImpl), calls };
}

function setVisibility(state: "visible" | "hidden") {
  Object.defineProperty(document, "visibilityState", { value: state, configurable: true });
}

describe("render cost", () => {
  test("the clock tick re-renders the clock, not the deck", async () => {
    const { client } = countingClient();
    const queries = render(<App client={client} initialRoute="overview" />);
    await waitFor(() => expect(queries.container.textContent).toContain("API LIVE"));
    // Let the mount-time probe and scramble animations settle.
    await act(() => new Promise((resolve) => setTimeout(resolve, 1200)));

    const app = renderCount("App");
    const page = renderCount("OverviewPage");
    const clock = renderCount("UtcClock");
    await act(() => new Promise((resolve) => setTimeout(resolve, 2100)));

    // act() batches the ticks into one commit; App owning the clock state
    // would still re-render once here, so the counts tell the cases apart.
    expect(renderCount("UtcClock")).toBeGreaterThan(clock);
    expect(renderCount("App")).toBe(app);
    expect(renderCount("OverviewPage")).toBe(page);
  });
});

describe("api call discipline", () => {
  test("an older list response never overwrites a newer one", async () => {
    // Two loads overlap: the first answers last. The table must show the
    // newer answer, and the spinner must clear only when that answer lands.
    const pending: Array<(items: SearchProfilePayload[]) => void> = [];
    const base = createApiClient(async (url: string) => ({
      ok: true,
      status: 200,
      json: async () => (url === "/healthz" ? { status: "ok", upstream: "x" } : health),
    }));
    const client: ApiClient = {
      ...base,
      listProfiles: () =>
        new Promise((resolve) => {
          pending.push((items) =>
            resolve({
              result: { ok: true, value: { items, total: items.length } },
              ms: 1,
              at: "2026-11-02T08:00:00Z",
            }),
          );
        }),
    };

    window.location.hash = "#/profiles";
    const queries = render(<App client={client} />);
    await waitFor(() => expect(pending.length).toBe(1));
    act(() => {
      window.location.hash = "#/overview";
      window.dispatchEvent(new Event("hashchange"));
    });
    act(() => {
      window.location.hash = "#/profiles";
      window.dispatchEvent(new Event("hashchange"));
    });
    await waitFor(() => expect(pending.length).toBe(2));

    await act(async () => pending[1]?.([profile("p2", "newer")]));
    await act(async () => pending[0]?.([profile("p1", "older")]));

    const table = queries.getByRole("table", { name: "Search profiles" });
    expect(table.textContent).toContain("newer");
    expect(table.textContent).not.toContain("older");
    expect(queries.getByRole("button", { name: "REFRESH" })).toBeTruthy();
  });

  test("a hidden deck skips the poll; showing it again probes once", async () => {
    const { client, calls } = countingClient();
    const queries = render(<App client={client} initialRoute="overview" />);
    await waitFor(() => expect(queries.container.textContent).toContain("API LIVE"));
    expect(calls["/api/v1/health"]).toBe(1);

    // Back before a full interval: no extra probe.
    setVisibility("hidden");
    act(() => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    setVisibility("visible");
    act(() => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    expect(calls["/api/v1/health"]).toBe(1);

    // Back after the interval: the deck catches up with one probe.
    setVisibility("hidden");
    setSystemTime(new Date(Date.now() + PROBE_INTERVAL_MS + 1000));
    setVisibility("visible");
    await act(async () => {
      document.dispatchEvent(new Event("visibilitychange"));
    });
    await waitFor(() => expect(calls["/api/v1/health"]).toBe(2));
  });

  test("shouldProbe polls only a visible deck whose last probe is stale", () => {
    expect(shouldProbe("visible", 0, PROBE_INTERVAL_MS)).toBe(true);
    expect(shouldProbe("visible", 1000, 1000 + PROBE_INTERVAL_MS / 2)).toBe(false);
    expect(shouldProbe("hidden", 0, PROBE_INTERVAL_MS * 3)).toBe(false);
  });

  test("an unreadable body is a problem, and the next probe still runs", async () => {
    let healthCalls = 0;
    const client = createApiClient(async (url: string) => {
      if (url === "/healthz") {
        return { ok: true, status: 200, json: async () => ({ status: "ok", upstream: "x" }) };
      }
      healthCalls += 1;
      return {
        ok: true,
        status: 200,
        json: async () => {
          throw new SyntaxError("Unexpected token '<'");
        },
      };
    });
    const queries = render(<App client={client} initialRoute="health" />);
    await waitFor(() => expect(queries.getByRole("alert").textContent).toContain("BAD_RESPONSE"));

    fireEvent.click(queries.getByRole("button", { name: "RUN HEALTH CHECK" }));
    await waitFor(() => expect(healthCalls).toBe(2));
  });
});
