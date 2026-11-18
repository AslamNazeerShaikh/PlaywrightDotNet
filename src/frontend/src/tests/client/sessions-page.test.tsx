import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render } from "@testing-library/react";

import type { CreateSessionInput, SourceList, SourceStatus } from "../../client/lib/api.ts";
import { SessionsPage } from "../../client/pages/sessions.tsx";

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

const apiSource: SourceStatus = {
  key: "naukri",
  apiOnly: true,
  tier: "Standard",
  enabled: true,
  gate: { allowed: true, reason: null },
  policy: {
    maxPages: 3,
    maxJobs: 60,
    maxDetails: 60,
    minDelayMs: 2500,
    maxDelayMs: 6000,
    challengeCooldownMinutes: 45,
  },
  quota: {
    date: "2026-11-02",
    pagesUsed: 0,
    pageCap: 25,
    detailsUsed: 0,
    detailCap: 60,
    runsUsed: 0,
    runCap: null,
    challenges: 0,
  },
  circuit: {
    state: "Closed",
    openUntil: null,
    canProbe: false,
    consecutiveFailures: 0,
    recentFailures: 0,
  },
};

const browserSource: SourceStatus = { ...apiSource, apiOnly: false };
const apiIndeed: SourceStatus = { ...apiSource, key: "indeed" };

function list(items: SourceStatus[]): SourceList {
  return { items, asOf: "2026-11-02T08:00:00Z" };
}

function renderPage(
  sources: SourceList | null,
  onCreate: (input: CreateSessionInput) => Promise<boolean> = async () => false,
) {
  return render(
    <SessionsPage
      sessions={[]}
      listProblem={null}
      loading={false}
      creating={false}
      createProblem={null}
      selectedId={null}
      onSelect={() => undefined}
      onCreate={onCreate}
      onUpdate={async () => false}
      onRefresh={() => undefined}
      updating={false}
      updateProblem={null}
      onUnauthorized={() => undefined}
      sources={sources}
    />,
  );
}

describe("sessions page transport", () => {
  test("an API-only source offers no browser profile creation", () => {
    const queries = renderPage(list([apiSource]));

    expect(queries.queryByLabelText("Profile name")).toBeNull();
    expect(queries.queryByLabelText("Engine")).toBeNull();
    expect(queries.queryByLabelText("Channel")).toBeNull();
    expect(queries.queryByRole("button", { name: "CREATE BROWSER PROFILE" })).toBeNull();
    expect(queries.getByText("API", { selector: ".code-tag" })).toBeDefined();
    expect(queries.getByText(/runs API-only/)).toBeDefined();
  });

  test("a browser source keeps the creation form byte-identical", () => {
    const queries = renderPage(list([browserSource]));

    expect(queries.getByLabelText("Profile name")).toBeDefined();
    expect(queries.getByLabelText("Engine")).toBeDefined();
    expect(queries.getByLabelText("Channel")).toBeDefined();
    expect(queries.getByRole("button", { name: "CREATE BROWSER PROFILE" })).toBeDefined();
    expect(queries.queryByText(/runs API-only/)).toBeNull();
  });

  test("switching the form's source to an API-only source hides creation", () => {
    const queries = renderPage(list([browserSource, apiIndeed]));

    expect(queries.getByLabelText("Profile name")).toBeDefined();
    fireEvent.change(queries.getByLabelText("Source"), { target: { value: "indeed" } });

    expect(queries.queryByLabelText("Profile name")).toBeNull();
    expect(queries.getByText(/runs API-only/)).toBeDefined();

    fireEvent.change(queries.getByLabelText("Source"), { target: { value: "naukri" } });
    expect(queries.getByLabelText("Profile name")).toBeDefined();
  });

  test("an unknown sources snapshot keeps the creation form", () => {
    const queries = renderPage(null);

    expect(queries.getByLabelText("Profile name")).toBeDefined();
  });

  test("an API-only form never calls create, even if the form submits", () => {
    const calls: CreateSessionInput[] = [];
    const queries = renderPage(list([apiSource]), async (input) => {
      calls.push(input);
      return true;
    });

    fireEvent.submit(queries.getByRole("form", { name: "New browser profile" }));

    expect(calls).toEqual([]);
  });
});
