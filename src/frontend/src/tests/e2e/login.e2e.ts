import { expect, test } from "@playwright/test";

/**
 * The deck is auth-gated: with no session cookie every route renders the sign-in page. The heading
 * text decrypts via Scramble, so the suite pins the instant `aria-label` (same string) instead of
 * waiting out the animation, plus the stable module codes and form controls.
 */
test("default route renders the sign-in page", async ({ page }) => {
  await page.goto("/");

  await expect(page.getByLabel("OPS-DECK // SIGN IN")).toBeVisible();
  await expect(page.locator("form[aria-label='Sign in']")).toBeVisible();
  await expect(page.getByLabel("Email", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Password", { exact: true })).toBeVisible();
  await expect(page.getByRole("button", { name: "SIGN IN" })).toBeVisible();
  await expect(page.getByText("AUTH-01")).toBeVisible();
  await expect(page.getByText("AUTH-02")).toBeVisible();
  await expect(page.getByRole("button", { name: "First login / setup password" })).toBeVisible();
});

test("overview route renders sign-in when unauthenticated", async ({ page }) => {
  await page.goto("/#/overview");

  await expect(page.getByLabel("OPS-DECK // SIGN IN")).toBeVisible();
  await expect(page.locator("form[aria-label='Sign in']")).toBeVisible();
});
