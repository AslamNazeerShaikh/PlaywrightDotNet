import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, render } from "@testing-library/react";

import { Compass } from "../../client/components/compass.tsx";

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

describe("compass", () => {
  test("shows the padded heading and page label", () => {
    const queries = render(<Compass heading={90} page="ENGINE HEALTH" />);

    expect(queries.getByLabelText("Heading 90 degrees, ENGINE HEALTH")).toBeDefined();
    expect(queries.getByText("090°")).toBeDefined();
    expect(queries.getByText("ENGINE HEALTH")).toBeDefined();
  });

  test("pads single-digit headings", () => {
    const queries = render(<Compass heading={5} page="OVERVIEW" />);

    expect(queries.getByText("005°")).toBeDefined();
  });
});
