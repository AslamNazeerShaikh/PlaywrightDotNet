/** Shared e2e endpoints and seeded credentials, exported by playwright.config.ts. */

export const BASE_URL = process.env.JOBSCRAPE_E2E_BASE_URL ?? "http://127.0.0.1:3399";
export const API_URL = process.env.JOBSCRAPE_E2E_API_URL ?? "http://127.0.0.1:5399";
export const ADMIN_EMAIL = process.env.JOBSCRAPE_E2E_ADMIN_EMAIL ?? "admin@example.com";
export const ADMIN_SEED_SECRET = process.env.JOBSCRAPE_E2E_ADMIN_SEED_SECRET ?? "Setup-Test-12!!";
export const ADMIN_PASSWORD = process.env.JOBSCRAPE_E2E_ADMIN_PASSWORD ?? "E2e-Admin-1!!";
