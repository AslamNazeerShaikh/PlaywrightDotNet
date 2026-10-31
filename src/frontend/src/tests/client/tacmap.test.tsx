import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, render } from "@testing-library/react";

import { TacMap } from "../../client/components/tacmap.tsx";

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

const samples = [
  { label: "health", ms: 12, at: 1, ok: true },
  { label: "health", ms: 41, at: 2, ok: false },
];

describe("tac-map", () => {
  test("plots HQ, cardinal units, and contacts", () => {
    const queries = render(<TacMap samples={samples} apiState="live" />);

    expect(queries.getByLabelText("Tactical overview map", { exact: false })).toBeDefined();
    expect(queries.getByText("BFF :3000")).toBeDefined();
    expect(queries.getByText(".NET :5207")).toBeDefined();
    expect(queries.getByText("HQ")).toBeDefined();
  });

  test("unknown plane renders without tracers but keeps the sweep", () => {
    const queries = render(<TacMap samples={[]} apiState="unknown" />);

    expect(queries.getByLabelText("no contacts yet", { exact: false })).toBeDefined();
  });
});
