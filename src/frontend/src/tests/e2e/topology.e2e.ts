import { expect, test } from "@playwright/test";

import { signInDeck } from "./support.ts";

/**
 * Topology page. Same session situation as the database page: an anonymous run hits the sign-in
 * gate. Here the diagram assertions must actually execute, so the anonymous branch signs in with
 * the seeded admin instead of returning early — the diagrams are the reason this spec exists.
 */

test("topology page: current scrape flow and new-only delta diagrams render", async ({ page }) => {
  await page.goto("/#/topology");

  const signIn = page.locator("form[aria-label='Sign in']");
  // The shell renders "Restoring session…" first; wait for either settled
  // state before branching so the gate read cannot race the restore.
  await expect(signIn.or(page.getByLabel("OPS-DECK // TOPOLOGY", { exact: true }))).toBeVisible();
  if ((await signIn.count()) > 0) {
    await signInDeck(page);
    await page.goto("/#/topology");
  }

  await expect(page.getByLabel("OPS-DECK // TOPOLOGY")).toBeVisible();
  await expect(page.getByText("TOP-05").first()).toBeVisible();
  await expect(page.getByText("TOP-06").first()).toBeVisible();
  await expect(page.getByRole("img", { name: "Current scrape flow" })).toBeVisible();
  await expect(page.getByRole("img", { name: "New-only run delta" })).toBeVisible();
});
