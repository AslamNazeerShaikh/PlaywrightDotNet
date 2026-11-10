import { expect, type Page, request } from "@playwright/test";

/** Shared e2e endpoints and seeded credentials, exported by playwright.config.ts. */

export const BASE_URL = process.env.JOBSCRAPE_E2E_BASE_URL ?? "http://127.0.0.1:3399";
export const API_URL = process.env.JOBSCRAPE_E2E_API_URL ?? "http://127.0.0.1:5399";
export const ADMIN_EMAIL = process.env.JOBSCRAPE_E2E_ADMIN_EMAIL ?? "admin@example.com";
export const ADMIN_SEED_SECRET = process.env.JOBSCRAPE_E2E_ADMIN_SEED_SECRET ?? "Setup-Test-12!!";
export const ADMIN_PASSWORD = process.env.JOBSCRAPE_E2E_ADMIN_PASSWORD ?? "E2e-Admin-1!!";

/**
 * Signs the deck in as the seeded admin. In the self-contained suite the auth spec configures the
 * admin first, so the first sign-in succeeds. On a standalone run of a spec on a fresh database the
 * admin still holds only its seeded secret and the backend answers INVALID_CREDENTIALS: the rescue
 * trades the secret for the operator password once, then the retried sign-in lands on the deck.
 * Login and setup-password share one fixed per-IP budget per minute, so this must not spend a setup
 * call the already-configured admin does not need.
 */
export async function signInDeck(page: Page): Promise<void> {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(ADMIN_EMAIL);
  await page.getByLabel("Password", { exact: true }).fill(ADMIN_PASSWORD);
  await page.getByRole("button", { name: "SIGN IN" }).click();
  const signOut = page.getByRole("button", { name: "SIGN OUT" });
  const alert = page.getByRole("alert");
  await expect(signOut.or(alert)).toBeVisible();
  if ((await signOut.count()) > 0) {
    return;
  }
  await expect(alert).toContainText("INVALID_CREDENTIALS");
  const bff = await request.newContext({ baseURL: BASE_URL });
  try {
    const setup = await bff.post("/bff/session/setup", {
      headers: { origin: BASE_URL, "sec-fetch-site": "same-origin" },
      data: {
        email: ADMIN_EMAIL,
        currentSecret: ADMIN_SEED_SECRET,
        newPassword: ADMIN_PASSWORD,
      },
    });
    expect([200, 409]).toContain(setup.status());
  } finally {
    await bff.dispose();
  }
  await page.getByRole("button", { name: "SIGN IN" }).click();
  await expect(signOut).toBeVisible();
}
