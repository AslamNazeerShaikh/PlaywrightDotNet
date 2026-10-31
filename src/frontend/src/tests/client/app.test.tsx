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

describe("App", () => {
  test("health check renders the healthy payload", async () => {
    const client = stubClient(() => ({
      status: 200,
      body: { status: "healthy", timeUtc: "today" },
    }));
    // Queries come from `render`, not `screen`: `screen` binds document.body
    // at import time, before the registration above has run.
    const queries = render(<App client={client} />);

    fireEvent.click(queries.getByRole("button", { name: "Check health" }));

    await waitFor(() => expect(queries.getByRole("status").textContent).toContain("healthy"));
  });

  test("profile lookup renders the profile and problems by code", async () => {
    const client = stubClient((url) =>
      url.endsWith("/nope")
        ? {
            status: 404,
            body: {
              status: 404,
              title: "Search profile not found.",
              detail: "Missing.",
              code: "PROFILE_NOT_FOUND",
            },
          }
        : { status: 200, body: { id: "x", name: "Backend", sourceKey: "naukri" } },
    );
    const queries = render(<App client={client} />);

    fireEvent.change(queries.getByLabelText("Profile id"), { target: { value: "x" } });
    fireEvent.click(queries.getByRole("button", { name: "Look up" }));
    await waitFor(() => expect(queries.getByRole("status").textContent).toContain("Backend"));

    fireEvent.change(queries.getByLabelText("Profile id"), {
      target: { value: "nope" },
    });
    fireEvent.click(queries.getByRole("button", { name: "Look up" }));
    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("PROFILE_NOT_FOUND"),
    );
  });

  test("empty lookup explains the requirement with the validation code", async () => {
    const client = stubClient(() => ({ status: 200, body: {} }));
    const queries = render(<App client={client} />);

    fireEvent.click(queries.getByRole("button", { name: "Look up" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("VALIDATION_FAILED"),
    );
  });
});
