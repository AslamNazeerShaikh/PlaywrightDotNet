import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render } from "@testing-library/react";

import { RulerTabs } from "../../client/components/rulertabs.tsx";

// Scoped happy-dom registration (see app.test.tsx): undone afterwards so the
// BFF server tests keep native globals.
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

describe("ruler tabs", () => {
  test("lists every page with section codes, current marked", () => {
    const queries = render(<RulerTabs route="health" />);

    const stops = queries.getAllByRole("link");
    expect(stops.map((s) => s.textContent)).toEqual([
      "01OVERVIEW",
      "02ENGINE HEALTH",
      "03SEARCH PROFILES",
      "04SOURCES",
      "05TOPOLOGY",
    ]);
    expect(queries.getByText("NAV // 05 STOPS")).toBeDefined();
    expect(
      queries.getByRole("link", { name: "02 ENGINE HEALTH" }).getAttribute("aria-current"),
    ).toBe("page");
  });

  test("blip sits on the active stop with section digits", () => {
    const first = render(<RulerTabs route="overview" />);
    expect(first.getByText("01/05")).toBeDefined();
    cleanup();

    const last = render(<RulerTabs route="topology" />);
    expect(last.getByText("05/05")).toBeDefined();
  });

  test("selecting a stop navigates", () => {
    let selected: unknown = null;
    const queries = render(
      <RulerTabs
        route="overview"
        onSelect={(r) => {
          selected = r;
        }}
      />,
    );

    fireEvent.click(queries.getByRole("link", { name: "03 SEARCH PROFILES" }));
    expect(selected).toBe("profiles");
  });
});
