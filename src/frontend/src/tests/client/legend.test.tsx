import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, render } from "@testing-library/react";

import {
  Abbr,
  Cross,
  Diamond,
  Dot,
  Glossary,
  Legend,
  Ring,
  Wire,
} from "../../client/components/legend.tsx";

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

describe("deck key", () => {
  test("legend lists every swatch with its meaning", () => {
    const queries = render(
      <Legend
        label="Test key"
        items={[
          { key: "a", swatch: <Dot color="var(--sig)" />, text: "hit" },
          { key: "b", swatch: <Diamond color="var(--sig)" />, text: "unit" },
          { key: "c", swatch: <Wire color="var(--sig)" />, text: "wire" },
          { key: "d", swatch: <Ring color="var(--sig)" />, text: "ring" },
          { key: "e", swatch: <Cross color="var(--red)" />, text: "kill" },
        ]}
      />,
    );

    expect(queries.getByRole("list", { name: "Test key" })).toBeDefined();
    for (const text of ["hit", "unit", "wire", "ring", "kill"]) {
      expect(queries.getByText(text)).toBeDefined();
    }
  });

  test("glossary expands each abbreviation with a hint", () => {
    const queries = render(
      <Glossary
        terms={[{ abbr: "BFF", full: "Backend for Frontend", hint: "Serves the deck." }]}
      />,
    );

    const term = queries.getByText("BFF");
    expect(term.tagName.toLowerCase()).toBe("abbr");
    expect(term.getAttribute("title")).toBe("Backend for Frontend");
    expect(queries.getByText("Serves the deck.", { exact: false })).toBeDefined();
  });

  test("abbr marks first mentions with a native tooltip", () => {
    const queries = render(<Abbr term="HQ" full="Headquarters" />);

    const term = queries.getByText("HQ");
    expect(term.getAttribute("title")).toBe("Headquarters");
  });
});
