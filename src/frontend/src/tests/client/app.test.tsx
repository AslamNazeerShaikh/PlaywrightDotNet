import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import { App } from "../../client/app.tsx";
import { createApiClient, type FetchImpl, type SessionPayload } from "../../client/lib/api.ts";
import type { DeckRoute } from "../../client/lib/router.ts";

// happy-dom replaces globals (Response, Request, fetch) that the BFF server
// tests need native, so registration is scoped to this file and undone
// afterwards instead of living in a global preload.
beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  // Drain React's scheduler queue while the DOM globals still exist; queued
  // work that runs after unregister would hit `window is not defined`.
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
});

function stubClient(handler: (url: string) => { status: number; body: unknown }) {
  const fetchImpl: FetchImpl = async (url: string) => {
    const { status, body } = handler(url);
    return {
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    };
  };
  return createApiClient(fetchImpl);
}

const savedProfile = {
  id: "p1",
  name: "backend",
  sourceKey: "naukri",
  keywords: "asp.net core",
  locations: ["Pune", "Hyderabad"],
  experienceYears: 6,
  postedWithinDays: 7,
  remoteOnly: false,
  requiredSkills: ["C#"],
  maxPages: 3,
  maxJobs: 60,
  maxDetails: 60,
  minDelayMs: 2500,
  maxDelayMs: 6000,
  enabled: true,
  createdAt: "2026-11-02T08:00:00Z",
  updatedAt: "2026-11-02T08:00:00Z",
};

const sourceList = {
  asOf: "2026-11-02T08:00:00Z",
  items: [
    {
      key: "indeed",
      tier: "Restricted",
      enabled: false,
      gate: {
        allowed: false,
        reason: "Source 'indeed' is restricted: set AcknowledgeSourceRisk=true to opt in.",
      },
      policy: {
        maxPages: 2,
        maxJobs: 40,
        maxDetails: 30,
        minDelayMs: 12000,
        maxDelayMs: 25000,
        challengeCooldownMinutes: 60,
      },
      quota: {
        date: "2026-11-02",
        pagesUsed: 0,
        pageCap: 12,
        detailsUsed: 0,
        detailCap: 30,
        runsUsed: 0,
        runCap: 1,
        challenges: 0,
      },
      circuit: {
        state: "Closed",
        openUntil: null,
        canProbe: false,
        consecutiveFailures: 0,
        recentFailures: 0,
      },
    },
    {
      key: "naukri",
      tier: "Standard",
      enabled: true,
      gate: { allowed: true, reason: null },
      policy: {
        maxPages: 3,
        maxJobs: 60,
        maxDetails: 60,
        minDelayMs: 2500,
        maxDelayMs: 6000,
        challengeCooldownMinutes: 45,
      },
      quota: {
        date: "2026-11-02",
        pagesUsed: 20,
        pageCap: 25,
        detailsUsed: 60,
        detailCap: 60,
        runsUsed: 1,
        runCap: null,
        challenges: 1,
      },
      circuit: {
        state: "Open",
        openUntil: "2026-11-03T08:00:00Z",
        canProbe: false,
        consecutiveFailures: 1,
        recentFailures: 1,
      },
    },
  ],
};

const healthyHandler = (url: string) => {
  if (url === "/healthz") {
    return { status: 200, body: { status: "ok", upstream: "http://127.0.0.1:5207" } };
  }
  if (url === "/api/v1/search-profiles") {
    return { status: 200, body: { items: [savedProfile], total: 1 } };
  }
  if (url === "/api/v1/sources") {
    return { status: 200, body: sourceList };
  }
  return { status: 200, body: { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" } };
};

/** A stateful fake of the sessions API with one cooling-down profile already saved. */
function sessionsBackend() {
  const items: SessionPayload[] = [
    {
      id: "s0",
      name: "cooling",
      sourceKey: "indeed",
      engine: "Chromium",
      channel: "Bundled",
      state: "CoolingDown",
      userDataDirectory: "indeed/cooling",
      canLaunch: false,
      cooldownUntil: "2026-11-02T09:00:00Z",
      consecutiveChallenges: 1,
      lastUsedAt: null,
      lastVerifiedAt: null,
    },
  ];
  const fetchImpl: FetchImpl = async (url: string, init?: RequestInit) => {
    const reply = (status: number, body: unknown) => ({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    });
    if (url === "/healthz") {
      return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
    }
    if (url.startsWith("/api/v1/sessions/") && init?.method === "PATCH") {
      const id = url.slice("/api/v1/sessions/".length);
      const body = JSON.parse(String(init.body)) as {
        name: string;
        engine: SessionPayload["engine"];
        channel: SessionPayload["channel"];
      };
      const index = items.findIndex((s) => s.id === id);
      if (index === -1) {
        return reply(404, {
          status: 404,
          title: "Browser profile not found.",
          detail: "",
          code: "SESSION_NOT_FOUND",
        });
      }
      if (items.some((s, i) => i !== index && s.name === body.name)) {
        return reply(409, {
          status: 409,
          title: "Browser profile name already in use.",
          detail: "",
          code: "SESSION_NAME_TAKEN",
        });
      }
      const updated: SessionPayload = {
        ...items[index]!,
        ...body,
        userDataDirectory: `${items[index]!.sourceKey}/${body.name}`,
      };
      items[index] = updated;
      return reply(200, updated);
    }
    if (url === "/api/v1/sessions" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as { name: string; sourceKey: string };
      if (body.name.includes("/")) {
        return reply(400, {
          status: 400,
          title: "One or more validation errors occurred.",
          detail: "The request contains invalid fields.",
          code: "VALIDATION_FAILED",
          errors: { name: ["Use 1 to 64 letters, digits, '.', '_', or '-'."] },
        });
      }
      if (items.some((s) => s.name === body.name)) {
        return reply(409, {
          status: 409,
          title: "Browser profile name already in use.",
          detail: "",
          code: "SESSION_NAME_TAKEN",
        });
      }
      const created: SessionPayload = {
        ...items[0]!,
        id: `s${items.length}`,
        name: body.name,
        sourceKey: body.sourceKey,
        state: "New",
        canLaunch: true,
        cooldownUntil: null,
        consecutiveChallenges: 0,
        userDataDirectory: `${body.sourceKey}/${body.name}`,
      };
      items.push(created);
      return reply(201, created);
    }
    if (url === "/api/v1/sessions") {
      return reply(200, { items, total: items.length });
    }
    return reply(200, { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" });
  };
  return createApiClient(fetchImpl);
}

/**
 * A stateful fake of the profiles API: GET lists the seeded and created rows, POST validates like
 * the server (missing name → VALIDATION_FAILED with field errors, duplicate → PROFILE_NAME_TAKEN),
 * and PUT replaces the editable fields.
 */
function profilesBackend(seed: (typeof savedProfile)[] = []) {
  const items: (typeof savedProfile)[] = [...seed];
  const fetchImpl: FetchImpl = async (url: string, init?: RequestInit) => {
    const reply = (status: number, body: unknown) => ({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    });
    if (url === "/healthz") {
      return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
    }
    if (url.startsWith("/api/v1/search-profiles/") && init?.method === "PUT") {
      const id = url.slice("/api/v1/search-profiles/".length);
      const body = JSON.parse(String(init.body)) as Partial<typeof savedProfile>;
      const index = items.findIndex((p) => p.id === id);
      if (index === -1) {
        return reply(404, {
          status: 404,
          title: "Search profile not found.",
          detail: "",
          code: "PROFILE_NOT_FOUND",
        });
      }
      if (items.some((p, i) => i !== index && p.name === body.name)) {
        return reply(409, {
          status: 409,
          title: "Profile name already in use.",
          detail: "",
          code: "PROFILE_NAME_TAKEN",
        });
      }
      const updated = { ...items[index]!, ...body };
      items[index] = updated;
      return reply(200, updated);
    }
    if (url === "/api/v1/search-profiles" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as { name: string; keywords: string };
      if (body.name === "") {
        return reply(400, {
          status: 400,
          title: "One or more validation errors occurred.",
          detail: "The request contains invalid fields.",
          code: "VALIDATION_FAILED",
          errors: { name: ["A name of 1 to 200 characters is required."] },
        });
      }
      if (items.some((p) => p.name === body.name)) {
        return reply(409, {
          status: 409,
          title: "Profile name already in use.",
          detail: `A search profile named '${body.name}' already exists.`,
          code: "PROFILE_NAME_TAKEN",
        });
      }
      const created = { ...savedProfile, id: `id-${items.length + 1}`, ...body };
      items.push(created);
      return reply(201, created);
    }
    if (url === "/api/v1/search-profiles") {
      return reply(200, { items, total: items.length });
    }
    return reply(200, { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" });
  };
  return createApiClient(fetchImpl);
}

describe("ops deck", () => {
  test("meta fetch fires once per mount, not per render", async () => {
    let metaCalls = 0;
    const counting: FetchImpl = async (url: string) => {
      if (url === "/healthz") {
        metaCalls += 1;
      }
      return { ok: true, status: 200, json: async () => ({ status: "ok", upstream: "x" }) };
    };
    const client = createApiClient(counting);
    const queries = render(<App client={client} initialRoute="overview" />);
    await waitFor(() => expect(queries.container.textContent).toContain("BFF LIVE"));
    const afterMount = metaCalls;
    queries.rerender(<App client={client} initialRoute="overview" />);
    queries.rerender(<App client={client} initialRoute="overview" />);
    expect(metaCalls).toBe(afterMount);
  });

  test("tab rail lists one tab per feature with stable codes", async () => {
    const queries = await renderDeck("overview");

    const tabs = queries.getAllByRole("link");
    expect(tabs.map((t) => t.textContent)).toEqual([
      "01OVERVIEW",
      "02ENGINE HEALTH",
      "03SEARCH PROFILES",
      "04SOURCES",
      "05SESSIONS",
      "06RUNS",
      "07TOPOLOGY",
    ]);
  });

  test("health check renders the healthy payload and logs the event", async () => {
    const queries = await renderDeck("health");

    fireEvent.click(queries.getByRole("button", { name: "RUN HEALTH CHECK" }));

    await waitFor(() => expect(queries.getByRole("status").textContent).toContain("HEALTHY"));
    expect(queries.getByRole("status").textContent).toContain("2026-10-31T00:00:00Z");
  });

  test("health auto-probes on mount with no clicks", async () => {
    const queries = await renderDeck("health");

    await waitFor(() => expect(queries.getByRole("status").textContent).toContain("HEALTHY"));
  });

  test("profiles page lists saved profiles and shows detail on select", async () => {
    const queries = await renderDeck("profiles");

    await waitFor(() => expect(queries.getByRole("button", { name: "backend" })).toBeDefined());
    expect(queries.getByText("Select a profile name to see its detail.")).toBeDefined();

    fireEvent.click(queries.getByRole("button", { name: "backend" }));

    const detail = queries.getByRole("table", { name: "Profile detail" });
    expect(detail.textContent).toContain("Pune, Hyderabad");
    expect(detail.textContent).toContain("3 pages · 60 jobs · 60 details");
    expect(detail.textContent).toContain("2500–6000 ms");
  });

  test("creating a profile adds it to the list and selects it", async () => {
    const queries = render(<App client={profilesBackend()} initialRoute="profiles" />);
    await waitFor(() =>
      expect(queries.getByText("No profiles yet. Create one to get started.")).toBeDefined(),
    );

    fireEvent.change(queries.getByLabelText("Name"), { target: { value: "dotnet-pune" } });
    fireEvent.change(queries.getByLabelText("Keywords"), { target: { value: "c#" } });
    fireEvent.change(queries.getByLabelText("Locations"), { target: { value: "Pune" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE PROFILE" }));

    await waitFor(() => expect(queries.getByRole("button", { name: "dotnet-pune" })).toBeDefined());
    expect(queries.getByRole("table", { name: "Profile detail" }).textContent).toContain(
      "dotnet-pune",
    );
    expect(queries.queryByRole("alert")).toBeNull();
  });

  test("server field errors render inline under their inputs", async () => {
    const queries = render(<App client={profilesBackend()} initialRoute="profiles" />);
    await waitFor(() => expect(queries.container.textContent).toContain("BFF LIVE"));

    fireEvent.click(queries.getByRole("button", { name: "CREATE PROFILE" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("VALIDATION_FAILED"),
    );
    const note = queries.getByRole("note");
    expect(note.getAttribute("data-field")).toBe("name");
    expect(note.textContent).toBe("A name of 1 to 200 characters is required.");
  });

  test("a duplicate name shows the conflict code", async () => {
    const queries = render(<App client={profilesBackend()} initialRoute="profiles" />);
    await waitFor(() => expect(queries.container.textContent).toContain("BFF LIVE"));
    const create = () => {
      fireEvent.change(queries.getByLabelText("Name"), { target: { value: "dup" } });
      fireEvent.change(queries.getByLabelText("Keywords"), { target: { value: "c#" } });
      fireEvent.change(queries.getByLabelText("Locations"), { target: { value: "Pune" } });
      fireEvent.click(queries.getByRole("button", { name: "CREATE PROFILE" }));
    };

    create();
    await waitFor(() => expect(queries.getByRole("button", { name: "dup" })).toBeDefined());
    create();

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("PROFILE_NAME_TAKEN"),
    );
  });

  test("editing a profile updates the list and the detail", async () => {
    const queries = render(
      <App client={profilesBackend([savedProfile])} initialRoute="profiles" />,
    );
    await waitFor(() => expect(queries.getByRole("button", { name: "backend" })).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "backend" }));
    fireEvent.change(queries.getByLabelText("Edit name"), {
      target: { value: "backend-2" },
    });
    fireEvent.change(queries.getByLabelText("Edit keywords"), {
      target: { value: "asp.net" },
    });
    fireEvent.click(queries.getByRole("button", { name: "SAVE CHANGES" }));

    await waitFor(() => expect(queries.getByRole("button", { name: "backend-2" })).toBeDefined());
    expect(queries.getByRole("table", { name: "Profile detail" }).textContent).toContain("asp.net");
    expect(queries.queryByRole("alert")).toBeNull();
  });

  test("editing a profile to a taken name shows the conflict", async () => {
    const queries = render(
      <App
        client={profilesBackend([savedProfile, { ...savedProfile, id: "p2", name: "other" }])}
        initialRoute="profiles"
      />,
    );
    await waitFor(() => expect(queries.getByRole("button", { name: "backend" })).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "backend" }));
    fireEvent.change(queries.getByLabelText("Edit name"), {
      target: { value: "other" },
    });
    fireEvent.click(queries.getByRole("button", { name: "SAVE CHANGES" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("PROFILE_NAME_TAKEN"),
    );
  });

  test("sources page renders a card per source with gate, meters, and circuit", async () => {
    const queries = await renderDeck("sources");

    await waitFor(() => expect(queries.getByRole("region", { name: "NAUKRI" })).toBeDefined());
    const indeed = queries.getByRole("region", { name: "INDEED" });
    expect(indeed.textContent).toContain("RESTRICTED");
    expect(indeed.textContent).toContain("GATE BLOCKED");
    expect(indeed.textContent).toContain("AcknowledgeSourceRisk=true");
    const naukri = queries.getByRole("region", { name: "NAUKRI" });
    expect(naukri.textContent).toContain("GATE OPEN");
    expect(naukri.textContent).toContain("CIRCUIT OPEN");
    expect(naukri.textContent).toContain("Runs: 1 · no daily cap");
    expect(naukri.textContent).toContain("open until 2026-11-03T08:00:00Z");
    expect(naukri.textContent).toContain("3 pages · 60 jobs · 60 details");
    const meters = queries.getAllByRole("meter", { name: "Pages used" });
    expect(meters.map((m) => m.getAttribute("data-level"))).toEqual(["ok", "near"]);
    expect(
      queries.getAllByRole("meter", { name: "Details used" })[1]?.getAttribute("data-level"),
    ).toBe("full");
  });

  test("sessions page creates a profile, shows field errors, and flags blocked launches", async () => {
    const queries = render(<App client={sessionsBackend()} initialRoute="sessions" />);
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
        "COOLING_DOWN",
      ),
    );
    expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
      "BLOCKED",
    );

    fireEvent.change(queries.getByLabelText("Profile name"), { target: { value: "../bad" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE BROWSER PROFILE" }));
    await waitFor(() => expect(queries.getByRole("note").getAttribute("data-field")).toBe("name"));

    fireEvent.change(queries.getByLabelText("Profile name"), { target: { value: "naukri-main" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE BROWSER PROFILE" }));
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
        "naukri-main",
      ),
    );
    expect(queries.queryByRole("alert")).toBeNull();

    fireEvent.change(queries.getByLabelText("Profile name"), { target: { value: "naukri-main" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE BROWSER PROFILE" }));
    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("SESSION_NAME_TAKEN"),
    );
  });

  test("editing a browser profile moves its directory", async () => {
    const queries = render(<App client={sessionsBackend()} initialRoute="sessions" />);
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
        "cooling",
      ),
    );

    fireEvent.click(queries.getByRole("button", { name: "cooling" }));
    fireEvent.change(queries.getByLabelText("Edit profile name"), {
      target: { value: "renamed" },
    });
    fireEvent.click(queries.getByRole("button", { name: "SAVE CHANGES" }));

    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
        "indeed/renamed",
      ),
    );
    expect(queries.queryByRole("alert")).toBeNull();
  });

  test("editing a browser profile to a taken name shows the conflict", async () => {
    const queries = render(<App client={sessionsBackend()} initialRoute="sessions" />);
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
        "cooling",
      ),
    );

    fireEvent.change(queries.getByLabelText("Profile name"), { target: { value: "taken" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE BROWSER PROFILE" }));
    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Browser profiles" }).textContent).toContain(
        "taken",
      ),
    );

    fireEvent.click(queries.getByRole("button", { name: "cooling" }));
    fireEvent.change(queries.getByLabelText("Edit profile name"), {
      target: { value: "taken" },
    });
    fireEvent.click(queries.getByRole("button", { name: "SAVE CHANGES" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("SESSION_NAME_TAKEN"),
    );
  });

  test("overview shows the tac-map, its key, feed key, and catalog", async () => {
    const queries = await renderDeck("overview");

    expect(queries.getByLabelText("Tactical overview map", { exact: false })).toBeDefined();
    expect(queries.getByRole("list", { name: "Tac-map key" })).toBeDefined();
    expect(queries.getByRole("list", { name: "Killfeed key" })).toBeDefined();
    expect(queries.getByText("BFF_LINK")).toBeDefined();
    expect(queries.getByText("/api/v1/search-profiles/{id}")).toBeDefined();
    expect(queries.getByText("CreateSearchProfileHandler")).toBeDefined();
  });

  test("topology shows the lifecycle sequence, its key, and the glossary", async () => {
    const queries = await renderDeck("topology");

    expect(queries.getByLabelText("Request lifecycle sequence")).toBeDefined();
    expect(queries.getByRole("list", { name: "Sequence key" })).toBeDefined();
    expect(queries.getByText("Backend for Frontend", { exact: false })).toBeDefined();
    expect(queries.getByText("DOTNET_UNREACHABLE", { exact: false })).toBeDefined();
  });

  test("deck defaults to cozy density and toggles without losing content", async () => {
    const queries = await renderDeck("overview");

    const root = queries.container.firstElementChild;
    expect(root?.getAttribute("data-density")).toBe("cozy");
    expect(root?.getAttribute("data-theme")).toBe("light");
    fireEvent.click(queries.getByRole("button", { name: "DENSITY: COZY" }));
    expect(root?.getAttribute("data-density")).toBe("compact");
    expect(queries.getByLabelText("Endpoint catalog")).toBeDefined();
  });

  test("theme toggle flips to dark without losing content", async () => {
    const queries = await renderDeck("overview");

    fireEvent.click(queries.getByRole("button", { name: "THEME: LIGHT" }));
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(queries.container.firstElementChild?.getAttribute("data-theme")).toBe("dark");
    expect(queries.getByLabelText("Endpoint catalog")).toBeDefined();

    fireEvent.click(queries.getByRole("button", { name: "THEME: DARK" }));
    expect(document.documentElement.dataset.theme).toBe("light");
    document.documentElement.removeAttribute("data-theme");
  });
});

/**
 * Renders the deck and waits for the BFF self-check to settle, so the async /healthz state update
 * lands inside the test (act) instead of leaking into the next test's scheduler queue.
 */
async function renderDeck(route: DeckRoute) {
  const queries = render(<App client={stubClient(healthyHandler)} initialRoute={route} />);
  await waitFor(() => expect(queries.container.textContent).toContain("BFF LIVE"));
  return queries;
}
