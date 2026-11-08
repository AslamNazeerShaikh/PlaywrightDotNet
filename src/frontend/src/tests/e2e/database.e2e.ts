import { expect, request, test } from "@playwright/test";

import { BASE_URL } from "./support.ts";

/**
 * Database page. The page needs the Admin role. The self-contained suite boots a fresh DB and no
 * browser session starts signed in, so anonymously the test pins the deterministic shape: the auth
 * gate in the DOM plus the 401 UNAUTHENTICATED answer at the API. When a session exists (a prior
 * spec signed in), the same test pins the four modules on empty state and ERD content only if
 * tables are present — the ERD branch never forces rows into existence.
 */
test("database page: modules on empty state, ERD only when tables exist", async ({ page }) => {
  await page.goto("/#/database");

  const signIn = page.locator("form[aria-label='Sign in']");
  // The shell renders "Restoring session…" first; wait for either settled
  // state before branching so the gate read cannot race the restore.
  await expect(signIn.or(page.getByLabel("OPS-DECK // DATABASE", { exact: true }))).toBeVisible();
  if ((await signIn.count()) > 0) {
    await expect(page.getByLabel("OPS-DECK // SIGN IN")).toBeVisible();

    const api = await request.newContext({
      baseURL: BASE_URL,
      ignoreHTTPSErrors: true,
    });
    try {
      const response = await api.get("/api/v1/db/tables");
      expect(response.status()).toBe(401);
      const body = (await response.json()) as { code: string };
      expect(body.code).toBe("UNAUTHENTICATED");
    } finally {
      await api.dispose();
    }
    return;
  }

  await expect(page.getByLabel("OPS-DECK // DATABASE")).toBeVisible();
  for (const code of ["DB-00", "DB-01", "DB-02", "DB-03"]) {
    await expect(page.getByText(code).first()).toBeVisible();
  }
  await expect(page.getByRole("table", { name: "Database tables" })).toBeVisible();
  await expect(
    page.getByRole("img", { name: "Column-level ERD of the run subsystem" }),
  ).toBeVisible();

  const dataRows = page.locator("table[aria-label='Database tables'] tbody tr");
  if ((await dataRows.count()) > 0) {
    await expect(page.getByText(/Physical-FK subsystem: \d+ of \d+ live tables\./)).toBeVisible();
  } else {
    await expect(page.getByText("The run subsystem is not in the live schema yet.")).toBeVisible();
  }
});
