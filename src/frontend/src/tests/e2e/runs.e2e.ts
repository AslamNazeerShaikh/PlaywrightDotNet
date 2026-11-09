import { expect, type Page, test } from "@playwright/test";

import { signInDeck } from "./support.ts";

/**
 * Runs page. Same session situation as the database page: unauthenticated runs deterministically
 * hit the sign-in gate. When signed in, the suite pins the history headers on an empty runs view,
 * and the rank (#) / posted-date (Posted) assertions only when a postings table actually renders —
 * never forcing a scrape into existence.
 *
 * The new-only assertions sign in (the gate hides every control otherwise) and route the data
 * plane: the GETs and POST /api/v1/runs are fulfilled with canned payloads so no browser session or
 * scrape can exist, and the POST route captures the JSON body to pin `onlyNew` both ways. Behavior
 * past this surface is covered at API level (Task 5). Sign-in goes through the shared `signInDeck`
 * helper, which rescues the seeded admin on a standalone fresh database.
 */

const ENABLED_PROFILE = {
  id: "profile-1",
  name: "Backend roles",
  sourceKey: "naukri",
  keywords: "dotnet",
  locations: ["Pune"],
  experienceYears: null,
  postedWithinDays: null,
  remoteOnly: false,
  requiredSkills: [],
  companies: [],
  maxPages: 5,
  startPage: 1,
  maxJobs: 20,
  maxDetails: 10,
  minDelayMs: 500,
  maxDelayMs: 1500,
  enabled: true,
  createdAt: "2026-11-09T00:00:00Z",
  updatedAt: "2026-11-09T00:00:00Z",
};

const USABLE_SESSION = {
  id: "session-1",
  name: "Naukri session",
  sourceKey: "naukri",
  engine: "Chromium",
  channel: "Chrome",
  state: "Healthy",
  userDataDirectory: "/tmp/task10-session",
  canLaunch: true,
  cooldownUntil: null,
  consecutiveChallenges: 0,
  lastUsedAt: null,
  lastVerifiedAt: null,
};

const FLAGGED_POSTING = {
  id: "posting-1",
  externalJobId: "42",
  url: "https://example.invalid/job/42",
  title: "Senior .NET Engineer",
  company: "Acme Corp",
  location: "Pune",
  workMode: "Hybrid",
  experience: "5-8 yrs",
  salary: null,
  postedAt: "2026-11-08T00:00:00Z",
  skills: ["C#", "EF Core", "Playwright"],
  enriched: true,
  isNew: true,
  firstSeenAt: "2026-11-09T00:00:00Z",
  lastSeenAt: "2026-11-09T00:00:00Z",
};

interface RunWire {
  id: string;
  onlyNew: boolean;
  [key: string]: unknown;
}

function terminalRun(id: string, onlyNew: boolean): RunWire {
  return {
    id,
    profileId: "profile-1",
    profileName: "Backend roles",
    companies: [],
    sourceKey: "naukri",
    stage: "Completed",
    isTerminal: true,
    onlyNew,
    pagesFetched: 1,
    postingsFound: 3,
    postingsNew: onlyNew ? 3 : 1,
    postingsUpdated: onlyNew ? 0 : 2,
    detailsFetched: 2,
    challenges: 0,
    failures: 0,
    maxPages: 5,
    startPage: 1,
    maxJobs: 20,
    maxDetails: 10,
    pauseReason: null,
    retryAt: null,
    failureReason: null,
    createdAt: "2026-11-09T00:00:00Z",
    startedAt: "2026-11-09T00:00:01Z",
    finishedAt: "2026-11-09T00:00:42Z",
  };
}

/** Route the runs page's read model; `list` is what the history list and detail poll see. */
async function routeRunsPage(
  page: Page,
  list: RunWire[],
  onPost?: (body: Record<string, unknown>) => void,
): Promise<void> {
  await page.route(
    (url) => url.pathname === "/api/v1/search-profiles",
    (route) => route.fulfill({ json: { items: [ENABLED_PROFILE], total: 1 } }),
  );
  await page.route(
    (url) => url.pathname === "/api/v1/sessions",
    (route) => route.fulfill({ json: { items: [USABLE_SESSION], total: 1 } }),
  );
  const started = new Map<string, boolean>();
  await page.route(
    (url) => url.pathname === "/api/v1/runs",
    (route, request) => {
      if (request.method() === "POST") {
        const body = request.postDataJSON() as Record<string, unknown>;
        const onlyNew = body.onlyNew === true;
        started.set("run-started", onlyNew);
        onPost?.(body);
        return route.fulfill({ status: 201, json: terminalRun("run-started", onlyNew) });
      }
      return route.fulfill({ json: { items: list, total: list.length } });
    },
  );
  const ids = [...list.map((r) => r.id), "run-started"];
  for (const id of ids) {
    const onlyNew = () => list.find((r) => r.id === id)?.onlyNew ?? started.get(id) ?? false;
    await page.route(
      (url) => url.pathname === `/api/v1/runs/${id}`,
      (route) => route.fulfill({ json: terminalRun(id, onlyNew()) }),
    );
    await page.route(
      (url) => url.pathname === `/api/v1/runs/${id}/postings`,
      (route) => route.fulfill({ json: { runId: id, items: [], total: 0 } }),
    );
  }
}

test("runs page: headers on empty view, rank/posted only when postings render", async ({
  page,
}) => {
  await page.goto("/#/runs");

  const signIn = page.locator("form[aria-label='Sign in']");
  // The shell renders "Restoring session…" first; wait for either settled
  // state before branching so the gate read cannot race the restore.
  await expect(signIn.or(page.getByLabel("OPS-DECK // RUNS", { exact: true }))).toBeVisible();
  if ((await signIn.count()) > 0) {
    await expect(page.getByLabel("OPS-DECK // SIGN IN")).toBeVisible();
    return;
  }

  await expect(page.getByLabel("OPS-DECK // RUNS")).toBeVisible();
  await expect(page.getByText("RUN-01")).toBeVisible();
  await expect(page.getByText("RUN-02")).toBeVisible();

  const history = page.getByRole("table", { name: "Runs" });
  if ((await history.count()) > 0) {
    for (const header of [
      "Started (UTC)",
      "Profile",
      "Stage",
      "Pages",
      "Found · new",
      "Details",
      "Took",
    ]) {
      await expect(history.getByRole("columnheader", { name: header })).toBeVisible();
    }
  } else {
    await expect(page.getByText("No runs yet. Start one to see it here.")).toBeVisible();
  }

  const postings = page.getByRole("table", { name: "Postings" });
  if ((await postings.count()) > 0) {
    await expect(postings.getByRole("columnheader", { name: "#" })).toBeVisible();
    await expect(postings.getByRole("columnheader", { name: "Posted" })).toBeVisible();

    const ranks = await postings.locator("td.rank-cell").allTextContents();
    expect(ranks.length).toBeGreaterThan(0);
    ranks.forEach((rank, index) => expect(rank.trim()).toBe(String(index + 1)));

    const posted = await postings.locator("td.posted-cell").allTextContents();
    for (const cell of posted) {
      expect(cell.trim() === "—" || /^\d{4}-\d{2}-\d{2}$/.test(cell.trim())).toBe(true);
    }
    const whiteSpace = await postings
      .locator("td.posted-cell")
      .first()
      .evaluate((node) => getComputedStyle(node).whiteSpace);
    expect(whiteSpace).toBe("nowrap");

    // Posting links open the source posting in a new, untracked tab.
    const firstLink = postings.locator("tbody a").first();
    await expect(firstLink).toHaveAttribute("target", "_blank");
    await expect(firstLink).toHaveAttribute("rel", /noopener/);
    const href = await firstLink.getAttribute("href");
    expect(href).toMatch(/^https?:\/\//);

    // Unvisited posting links wear the signal color; --sig flips hex per theme,
    // so compare against a probe element instead of a literal.
    const sigColor = await page.evaluate(() => {
      const probe = document.createElement("span");
      probe.style.color = "var(--sig)";
      document.body.appendChild(probe);
      const color = getComputedStyle(probe).color;
      probe.remove();
      return color;
    });
    const linkColor = await firstLink.evaluate((node) => getComputedStyle(node).color);
    expect(linkColor).toBe(sigColor);
  }
});

test("runs page: flagged run badge, filtered postings note, NEW ONLY default off, tooltip, and POST body flag", async ({
  page,
}) => {
  const posts: Array<Record<string, unknown>> = [];
  await routeRunsPage(page, [terminalRun("run-flagged", true)], (body) => posts.push(body));
  await page.route(
    (url) => url.pathname === "/api/v1/runs/run-flagged/postings",
    (route) =>
      route.fulfill({ json: { runId: "run-flagged", items: [FLAGGED_POSTING], total: 1 } }),
  );
  await signInDeck(page);
  await page.goto("/#/runs");

  const runStages = page.getByRole("list", { name: "Run stages" });
  await expect(runStages).toBeVisible();
  await expect(runStages.getByText("NEW ONLY", { exact: true })).toBeVisible();
  await expect(
    page.getByText(
      "New only: the list holds postings first seen in this run, most recent first. Titles open the posting on the source.",
    ),
  ).toBeVisible();

  // Unvisited posting links wear the signal color; --sig flips hex per theme, so compare
  // against a probe element instead of a literal. Never click the link: it must stay unvisited.
  const firstLink = page.getByRole("link", { name: "Senior .NET Engineer" });
  await expect(firstLink).toBeVisible();
  const sigColor = await page.evaluate(() => {
    const probe = document.createElement("span");
    probe.style.color = "var(--sig)";
    document.body.appendChild(probe);
    const color = getComputedStyle(probe).color;
    probe.remove();
    return color;
  });
  const linkColor = await firstLink.evaluate((node) => getComputedStyle(node).color);
  expect(linkColor).toBe(sigColor);

  const onlyNew = page.getByLabel("New only", { exact: true });
  await expect(onlyNew).toBeVisible();
  await expect(onlyNew).not.toBeChecked();

  const tipButton = page
    .locator("label.check", { hasText: "NEW ONLY" })
    .getByRole("button", { name: "More info" });
  const tooltip = page.getByRole("tooltip", {
    name: "Keeps only postings first seen in this run.",
  });
  await tipButton.hover();
  await expect(tooltip).toBeVisible();
  await page.getByLabel("OPS-DECK // RUNS", { exact: true }).hover();
  await expect(tooltip).toBeHidden();
  await tipButton.focus();
  await expect(tooltip).toBeVisible();
  await tipButton.blur();
  await expect(tooltip).toBeHidden();

  await onlyNew.check();
  await page.getByRole("button", { name: "START RUN" }).click();
  await expect.poll(() => posts.length).toBe(1);
  expect(posts[0]?.onlyNew).toBe(true);

  await onlyNew.uncheck();
  await page.getByRole("button", { name: "START RUN" }).click();
  await expect.poll(() => posts.length).toBe(2);
  expect(posts[1]?.onlyNew).toBe(false);
});
