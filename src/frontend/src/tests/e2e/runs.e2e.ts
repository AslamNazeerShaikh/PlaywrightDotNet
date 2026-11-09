import { expect, test } from "@playwright/test";

/**
 * Runs page. Same session situation as the database page: unauthenticated runs deterministically
 * hit the sign-in gate. When signed in, the suite pins the history headers on an empty runs view,
 * and the rank (#) / posted-date (Posted) assertions only when a postings table actually renders —
 * never forcing a scrape into existence.
 */
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
