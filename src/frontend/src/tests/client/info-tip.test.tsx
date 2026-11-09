import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor, within } from "@testing-library/react";

import { InfoTip } from "../../client/components/info-tip.tsx";
import type { FetchImpl } from "../../client/lib/api.ts";
import { createApiClient } from "../../client/lib/api.ts";
import { RunsPage } from "../../client/pages/runs.tsx";

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

describe("InfoTip", () => {
  test("renders a circled i button with aria-describedby pointing at the tooltip text", () => {
    const queries = render(<InfoTip text="Keeps only postings first seen in this run." />);

    const button = queries.getByRole("button", { name: "More info" });
    expect(button.textContent).toBe("i");
    const tip = queries.getByRole("tooltip", { hidden: true });
    expect(button.getAttribute("aria-describedby")).toBe(tip.getAttribute("id"));
    expect(tip.textContent).toBe("Keeps only postings first seen in this run.");
  });

  test("tooltip appears on hover and on focus", () => {
    const queries = render(<InfoTip text="Keeps only postings first seen in this run." />);

    const button = queries.getByRole("button", { name: "More info" });
    const tip = queries.getByRole("tooltip", { hidden: true });
    expect(tip.hasAttribute("hidden")).toBe(true);

    fireEvent.mouseOver(button);
    expect(tip.hasAttribute("hidden")).toBe(false);
    fireEvent.mouseOut(button);
    expect(tip.hasAttribute("hidden")).toBe(true);

    fireEvent.focus(button);
    expect(tip.hasAttribute("hidden")).toBe(false);
    fireEvent.blur(button);
    expect(tip.hasAttribute("hidden")).toBe(true);
  });

  test("one-line usage copy for the NEW ONLY control exists on the runs page", async () => {
    const client = createApiClient(runsBackend());
    const queries = render(<RunsPage client={client} onEvent={() => undefined} />);
    await waitFor(() => expect(queries.getByRole("checkbox", { name: "New only" })).toBeDefined());

    const label = queries.getByRole("checkbox", { name: "New only" }).closest("label");
    expect(label).not.toBeNull();
    const button = within(label as HTMLElement).getByRole("button", { name: "More info" });
    const tip = within(label as HTMLElement).getByRole("tooltip", { hidden: true });
    expect(button.getAttribute("aria-describedby")).toBe(tip.getAttribute("id"));

    fireEvent.focus(button);
    expect(tip.hasAttribute("hidden")).toBe(false);
    expect(tip.textContent).toBe("Keeps only postings first seen in this run.");
  });
});

/** Minimal stand-in for the runs API: the form's options and an empty history. */
function runsBackend(): FetchImpl {
  return async (url: string, init?: RequestInit) => {
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
            locations: ["Pune"],
            experienceYears: 6,
            postedWithinDays: 1,
            remoteOnly: false,
            requiredSkills: ["C#"],
            companies: [],
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
    if (url.startsWith("/api/v1/runs")) {
      return reply(200, { items: [], total: 0 });
    }
    return reply(404, { status: 404, title: "Not found", detail: "", code: "NOT_FOUND" });
  };
}
