import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import { App } from "../../client/app.tsx";
import { createApiClient, type FetchImpl } from "../../client/lib/api.ts";

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

const healthyHandler = (url: string) => {
  if (url === "/healthz") {
    return { status: 200, body: { status: "ok", upstream: "http://127.0.0.1:5207" } };
  }
  if (url.startsWith("/api/v1/search-profiles/")) {
    return url.endsWith("/nope")
      ? {
          status: 404,
          body: {
            status: 404,
            title: "Search profile not found.",
            detail: "Missing.",
            code: "PROFILE_NOT_FOUND",
          },
        }
      : { status: 200, body: { id: "x", name: "Backend", sourceKey: "naukri" } };
  }
  return { status: 200, body: { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" } };
};

describe("ops deck", () => {
  test("tab rail lists one tab per feature with stable codes", async () => {
    const queries = await renderDeck("overview");

    const tabs = queries.getAllByRole("link");
    expect(tabs.map((t) => t.textContent)).toEqual([
      "01OVERVIEW",
      "02ENGINE HEALTH",
      "03SEARCH PROFILES",
      "04TOPOLOGY",
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

  test("profile lookup renders the profile, then the stable miss code", async () => {
    const queries = await renderDeck("profiles");

    fireEvent.change(queries.getByLabelText("Profile id"), { target: { value: "x" } });
    fireEvent.click(queries.getByRole("button", { name: "RUN LOOKUP" }));
    await waitFor(() => expect(queries.getByText("Backend")).toBeDefined());

    fireEvent.change(queries.getByLabelText("Profile id"), { target: { value: "nope" } });
    fireEvent.click(queries.getByRole("button", { name: "RUN LOOKUP" }));
    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("PROFILE_NOT_FOUND"),
    );
  });

  test("empty lookup explains the requirement with the validation code", async () => {
    const queries = await renderDeck("profiles");

    fireEvent.click(queries.getByRole("button", { name: "RUN LOOKUP" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("VALIDATION_FAILED"),
    );
  });

  test("overview shows the tac-map, its key, feed key, compass, and catalog", async () => {
    const queries = await renderDeck("overview");

    expect(queries.getByLabelText("Tactical overview map", { exact: false })).toBeDefined();
    expect(queries.getByRole("list", { name: "Tac-map key" })).toBeDefined();
    expect(queries.getByRole("list", { name: "Killfeed key" })).toBeDefined();
    expect(queries.getByLabelText("Heading 0 degrees, OVERVIEW")).toBeDefined();
    expect(queries.getByText("BFF_LINK")).toBeDefined();
    expect(queries.getByText("/api/v1/search-profiles/{id}")).toBeDefined();
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
async function renderDeck(route: "overview" | "health" | "profiles" | "topology") {
  const queries = render(<App client={stubClient(healthyHandler)} initialRoute={route} />);
  await waitFor(() => expect(queries.container.textContent).toContain("BFF LIVE"));
  return queries;
}
