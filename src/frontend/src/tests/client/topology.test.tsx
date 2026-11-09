import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, render } from "@testing-library/react";

import { type DiagramTone, TONE } from "../../client/components/diagram.tsx";
import { TopologyPage } from "../../client/pages/topology.tsx";

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

const renderPage = () => render(<TopologyPage upstream="http://localhost:5208" />);

describe("topology scrape diagrams", () => {
  test('renders TOP-05 with aria name "Current scrape flow"', () => {
    const queries = renderPage();
    const svg = queries.getByRole("img", { name: "Current scrape flow" });

    expect(svg).toBeDefined();
    expect(queries.getByText("TOP-05")).toBeDefined();
  });

  test('renders TOP-06 with aria name "New-only run delta"', () => {
    const queries = renderPage();
    const svg = queries.getByRole("img", { name: "New-only run delta" });

    expect(svg).toBeDefined();
    expect(queries.getByText("TOP-06")).toBeDefined();
  });

  test("the new-only path uses the focal tone and dropped exits use gray", () => {
    const queries = renderPage();
    const svg = queries.getByRole("img", { name: "New-only run delta" });
    const focal = Array.from(svg.querySelectorAll('[data-path="focal"]'));
    const dropped = Array.from(svg.querySelectorAll('[data-path="drop"]'));

    expect(focal.length).toBeGreaterThan(0);
    expect(dropped.length).toBeGreaterThan(0);
    for (const el of focal) {
      expect(el.getAttribute("style")).toContain("var(--sig)");
    }
    for (const el of dropped) {
      expect(el.getAttribute("style")).toContain("var(--line-hi)");
    }
  });

  test("legends cover every tone used", () => {
    const queries = renderPage();
    for (const name of ["Current scrape flow", "New-only run delta"]) {
      const svg = queries.getByRole("img", { name });
      const tones = new Set(
        Array.from(svg.querySelectorAll("[data-tone]")).map(
          (el) => el.getAttribute("data-tone") as DiagramTone,
        ),
      );
      expect(tones.size).toBeGreaterThan(0);
      const legend = svg.closest("section")?.querySelector(".legend");

      expect(legend).not.toBeNull();
      const styled = Array.from(legend?.querySelectorAll("[style]") ?? []);
      for (const tone of tones) {
        const stroke = TONE[tone].stroke;
        const covered = styled.some((el) => (el.getAttribute("style") ?? "").includes(stroke));
        expect(covered, `${name} legend covers ${tone}`).toBe(true);
      }
    }
  });
});
