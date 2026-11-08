import { type APIRequestContext, expect, type Page, request, test } from "@playwright/test";

import { ADMIN_EMAIL, ADMIN_PASSWORD, ADMIN_SEED_SECRET, API_URL, BASE_URL } from "./support.ts";

/**
 * Checklist item 10 flows that are deterministic in a real browser: the sign-in gate (see
 * login.e2e.ts), first-login setup surviving a reload, a wrong current password keeping the
 * session, the HttpOnly session cookie driving the data plane, and a role change signing the
 * affected user out with a notice. The suite owns its temp backend, so the seeded admin password is
 * known.
 */

let adminApi: APIRequestContext;
let adminToken = "";

function bearer(): Record<string, string> {
  return { authorization: `Bearer ${adminToken}` };
}

async function loginUi(page: Page, email: string, password: string): Promise<void> {
  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "SIGN IN" }).click();
  await expect(page.getByRole("button", { name: "SIGN OUT" })).toBeVisible();
}

async function createPendingUser(email: string, temporaryPassword: string): Promise<string> {
  const created = await adminApi.post("/api/v1/users", {
    headers: bearer(),
    data: { email, temporaryPassword },
  });
  expect(created.status()).toBe(201);
  return ((await created.json()) as { id: string }).id;
}

async function completeSetup(
  email: string,
  currentSecret: string,
  newPassword: string,
): Promise<void> {
  const setup = await adminApi.post("/api/v1/auth/setup-password", {
    data: { email, currentSecret, newPassword },
  });
  expect(setup.status()).toBe(200);
}

test.beforeAll(async () => {
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

  adminApi = await request.newContext({ baseURL: API_URL });
  const login = await adminApi.post("/api/v1/auth/login", {
    data: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
  });
  expect(login.status()).toBe(200);
  adminToken = ((await login.json()) as { token: string }).token;
});

test.afterAll(async () => {
  await adminApi?.dispose();
});

test("HttpOnly session cookie authenticates the data plane; no bearer reaches JS", async ({
  page,
}) => {
  await loginUi(page, ADMIN_EMAIL, ADMIN_PASSWORD);

  const dataPlanePages: Array<[string, string]> = [
    ["#/profiles", "OPS-DECK // SEARCH PROFILES"],
    ["#/sources", "OPS-DECK // SOURCES"],
    ["#/sessions", "OPS-DECK // SESSIONS"],
  ];
  for (const [hash, heading] of dataPlanePages) {
    await page.goto(`/${hash}`);
    await expect(page.getByLabel(heading, { exact: true })).toBeVisible();
    await expect(page.getByText("UNAUTHENTICATED")).toHaveCount(0);
    if (hash === "#/profiles") {
      await expect(page.getByText("No profiles yet. Create one to get started.")).toBeVisible();
    }
  }

  const sessionStorageDump = await page.evaluate(() => JSON.stringify({ ...sessionStorage }));
  expect(sessionStorageDump).not.toContain("eyJ");
  const readableCookies = await page.evaluate(() => document.cookie);
  expect(readableCookies).toContain("jobscrape_csrf");
  expect(readableCookies).not.toContain("jobscrape_session");
});

test("first-login setup survives a reload and completes into the signed-in deck", async ({
  page,
}) => {
  const email = `first-login-${crypto.randomUUID()}@example.com`;
  await createPendingUser(email, "Temp-Secret1!!");

  await page.goto("/");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByRole("button", { name: "First login / setup password" }).click();
  await expect(page.getByRole("form", { name: "Set password" })).toBeVisible();
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(email);

  // The setup context (email) lives in sessionStorage; a full reload must
  // land back on Set password with the email prefilled, not bounce to sign in.
  await page.reload();
  await expect(page.getByRole("form", { name: "Set password" })).toBeVisible();
  await expect(page.getByLabel("Email", { exact: true })).toHaveValue(email);

  await page.getByLabel("Current secret", { exact: true }).fill("Temp-Secret1!!");
  await page.getByLabel("New password", { exact: true }).fill("First-Login-2@@");
  await page.getByRole("button", { name: "SET PASSWORD" }).click();

  await expect(page.getByRole("button", { name: "SIGN OUT" })).toBeVisible();
  await expect(page.getByText(email).first()).toBeVisible();
  const setupEmail = await page.evaluate(() => sessionStorage.getItem("jobscrape.setupEmail"));
  expect(setupEmail).toBeNull();
});

test("a wrong current password on change-password keeps the session", async ({ page }) => {
  await loginUi(page, ADMIN_EMAIL, ADMIN_PASSWORD);
  await page.goto("/#/users");
  await expect(page.getByRole("form", { name: "Change my password" })).toBeVisible();

  await page.getByLabel("Current password", { exact: true }).fill("Wrong-Password1!!");
  await page.getByLabel("New password", { exact: true }).fill("Another-Pass5%%");
  await page.getByRole("button", { name: "CHANGE PASSWORD" }).click();

  await expect(page.getByRole("alert")).toContainText("INVALID_CREDENTIALS");
  await expect(page.getByRole("button", { name: "SIGN OUT" })).toBeVisible();
  await expect(page.locator("form[aria-label='Sign in']")).toHaveCount(0);

  // The BFF keeps the session cookie on INVALID_CREDENTIALS, and a reload
  // restores the same signed-in operator.
  const session = await page.request.get(`${BASE_URL}/bff/session`);
  expect(session.status()).toBe(200);
  expect(((await session.json()) as { email?: string }).email).toBe(ADMIN_EMAIL);

  await page.reload();
  await expect(page.getByRole("button", { name: "SIGN OUT" })).toBeVisible();
});

test("a role change signs the affected user out with a notice", async ({ browser }) => {
  const email = `role-change-${crypto.randomUUID()}@example.com`;
  const id = await createPendingUser(email, "Temp-Secret1!!");
  await completeSetup(email, "Temp-Secret1!!", "Role-Change-3##");

  const context = await browser.newContext({ baseURL: BASE_URL, ignoreHTTPSErrors: true });
  try {
    const viewer = await context.newPage();
    await loginUi(viewer, email, "Role-Change-3##");

    const grant = await adminApi.post(`/api/v1/users/${id}/roles`, {
      headers: bearer(),
      data: { role: "Operator" },
    });
    expect(grant.status()).toBe(200);

    // The grant bumps the target's token version; the next restore through
    // the BFF clears the cookie and renders the session-ended notice.
    await viewer.reload();
    await expect(viewer.getByText("Your account changed. Sign in again.")).toBeVisible();
    await expect(viewer.locator("form[aria-label='Sign in']")).toBeVisible();
    await expect(viewer.getByRole("button", { name: "SIGN OUT" })).toHaveCount(0);
  } finally {
    await context.close();
  }
});
