import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, render, waitFor } from "@testing-library/react";

import { Scramble } from "../../client/components/scramble.tsx";

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

describe("scramble", () => {
  test("opens on glyphs, locks into the target text", async () => {
    const queries = render(<Scramble text="OPS-DECK" duration={300} />);
    const node = queries.getByLabelText("OPS-DECK");

    // Symbol-only glyph set: the opening frame can never equal the target.
    expect(node.textContent).not.toBe("OPS-DECK");
    await waitFor(() => expect(node.textContent).toBe("OPS-DECK"));
  });

  test("reduced motion renders the target instantly", async () => {
    const realMatchMedia = window.matchMedia;
    // @ts-expect-error test-only matchMedia stub.
    window.matchMedia = () => ({ matches: true });
    try {
      const queries = render(<Scramble text="OPS-DECK" duration={10_000} />);
      expect(queries.getByLabelText("OPS-DECK").textContent).toBe("OPS-DECK");
    } finally {
      window.matchMedia = realMatchMedia;
    }
  });
});
