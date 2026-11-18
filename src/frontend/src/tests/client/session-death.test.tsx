import { afterAll, afterEach, beforeAll, describe, expect, mock, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";
import { useState } from "react";

import { type ApiProblem, createApiClient, type FetchImpl } from "../../client/lib/api.ts";
import { ProfilesPage } from "../../client/pages/profiles.tsx";
import { RunsPage } from "../../client/pages/runs.tsx";
import { SessionsPage } from "../../client/pages/sessions.tsx";

// happy-dom replaces globals (Response, Request, fetch) that the BFF server
// tests need native, so registration is scoped to this file and undone
// afterwards instead of living in a global preload.
beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  // Drain React's scheduler queue while the DOM globals still exist.
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
});

const unauthorized: ApiProblem = {
  status: 401,
  title: "Your session ended. Sign in again to pick up any account changes.",
  detail: "",
  code: "UNAUTHENTICATED",
};

/** App-like harness: onCreate writes the failure the page receives as a prop. */
function ProfilesHarness({ onUnauthorized }: { onUnauthorized: (problem: ApiProblem) => void }) {
  const [createProblem, setCreateProblem] = useState<ApiProblem | null>(null);
  return (
    <ProfilesPage
      profiles={[]}
      listProblem={null}
      loading={false}
      creating={false}
      createProblem={createProblem}
      selectedId={null}
      onSelect={() => undefined}
      onCreate={async () => {
        setCreateProblem(unauthorized);
        return false;
      }}
      onUpdate={async () => false}
      onRefresh={() => undefined}
      updating={false}
      updateProblem={null}
      onUnauthorized={onUnauthorized}
    />
  );
}

function SessionsHarness({ onUnauthorized }: { onUnauthorized: (problem: ApiProblem) => void }) {
  const [createProblem, setCreateProblem] = useState<ApiProblem | null>(null);
  return (
    <SessionsPage
      sessions={[]}
      sources={null}
      listProblem={null}
      loading={false}
      creating={false}
      createProblem={createProblem}
      selectedId={null}
      onSelect={() => undefined}
      onCreate={async () => {
        setCreateProblem(unauthorized);
        return false;
      }}
      onUpdate={async () => false}
      onRefresh={() => undefined}
      updating={false}
      updateProblem={null}
      onUnauthorized={onUnauthorized}
    />
  );
}

function stubClient(
  handler: (url: string, init?: RequestInit) => { status: number; body: unknown },
) {
  const fetchImpl: FetchImpl = async (url: string, init?: RequestInit) => {
    const { status, body } = handler(url, init);
    return {
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    };
  };
  return createApiClient(fetchImpl);
}

describe("session death routes pages to sign-in", () => {
  test("creating a profile with UNAUTHENTICATED calls onUnauthorized", async () => {
    const onUnauthorized = mock((_problem: ApiProblem) => undefined);
    const queries = render(<ProfilesHarness onUnauthorized={onUnauthorized} />);

    fireEvent.change(queries.getByLabelText("Name"), { target: { value: "backend" } });
    fireEvent.change(queries.getByLabelText("Keywords"), { target: { value: "c#" } });
    fireEvent.change(queries.getByLabelText("Locations"), { target: { value: "Pune" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE PROFILE" }));

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledTimes(1));
    expect(onUnauthorized.mock.calls[0]?.[0]).toEqual(unauthorized);
  });

  test("creating a session with UNAUTHENTICATED calls onUnauthorized", async () => {
    const onUnauthorized = mock((_problem: ApiProblem) => undefined);
    const queries = render(<SessionsHarness onUnauthorized={onUnauthorized} />);

    fireEvent.change(queries.getByLabelText("Profile name"), { target: { value: "main" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE BROWSER PROFILE" }));

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledTimes(1));
    expect(onUnauthorized.mock.calls[0]?.[0]).toEqual(unauthorized);
  });

  test("loading runs with UNAUTHENTICATED calls onUnauthorized", async () => {
    const onUnauthorized = mock((_problem: ApiProblem) => undefined);
    const client = stubClient((url) => {
      if (url.startsWith("/api/v1/runs?")) {
        return {
          status: 401,
          body: {
            type: "https://jobscrape.local/problems/unauthorized",
            title: unauthorized.title,
            detail: "",
            code: "UNAUTHENTICATED",
          },
        };
      }
      return { status: 200, body: { items: [], total: 0 } };
    });

    render(<RunsPage client={client} onEvent={() => undefined} onUnauthorized={onUnauthorized} />);

    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledTimes(1));
    expect(onUnauthorized.mock.calls[0]?.[0]?.code).toBe("UNAUTHENTICATED");
  });
});
