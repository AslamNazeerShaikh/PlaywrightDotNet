import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { defineConfig } from "@playwright/test";

/**
 * Self-contained e2e: the suite boots its own temp backend (temp SQLite DB) and BFF on loopback
 * ports, then drives the bundled Chromium against them. The shared dev backend on :5208 and its
 * database are never touched, and no external server has to be running. Playwright's bundled
 * Chromium is used; install it once per machine with `bunx playwright install chromium`. The BFF
 * is plain HTTP here: loopback is a secure context and cookies work without the local TLS cert.
 */

const FRONTEND_ROOT = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(FRONTEND_ROOT, "../..");
const API_PROJECT = join(REPO_ROOT, "src/backend/JobScrape.Api/JobScrape.Api.csproj");

const API_PORT = Number(process.env.JOBSCRAPE_E2E_API_PORT ?? "5399");
const BFF_PORT = Number(process.env.JOBSCRAPE_E2E_BFF_PORT ?? "3399");
const ROOT = process.env.JOBSCRAPE_E2E_ROOT ?? join(tmpdir(), "jobscrape-e2e");
const API_URL = process.env.JOBSCRAPE_E2E_API_URL ?? `http://127.0.0.1:${API_PORT}`;
const BASE_URL = process.env.JOBSCRAPE_E2E_BASE_URL ?? `http://127.0.0.1:${BFF_PORT}`;
const ADMIN_EMAIL = "admin@example.com";
const ADMIN_SEED_SECRET = "Setup-Test-12!!";
const ADMIN_PASSWORD = "E2e-Admin-1!!";
const JWT_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

// Workers inherit this environment from the config process, so the support module and the global
// teardown read the same root the servers were started with.
process.env.JOBSCRAPE_E2E_ROOT = ROOT;
process.env.JOBSCRAPE_E2E_API_URL = API_URL;
process.env.JOBSCRAPE_E2E_BASE_URL = BASE_URL;
process.env.JOBSCRAPE_E2E_ADMIN_EMAIL = ADMIN_EMAIL;
process.env.JOBSCRAPE_E2E_ADMIN_SEED_SECRET = ADMIN_SEED_SECRET;
process.env.JOBSCRAPE_E2E_ADMIN_PASSWORD = ADMIN_PASSWORD;

const childEnv = Object.fromEntries(
  Object.entries(process.env).filter((entry): entry is [string, string] => entry[1] !== undefined),
);

export default defineConfig({
  testDir: "./src/tests/e2e",
  testMatch: "**/*.e2e.ts",
  timeout: 30_000,
  retries: 0,
  workers: 1,
  outputDir: "./test-results",
  reporter: "list",
  globalTeardown: "./src/tests/e2e/global-teardown.ts",
  use: {
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: "chromium",
      use: {},
    },
  ],
  webServer: [
    {
      command: `dotnet run --project "${API_PROJECT}" --no-launch-profile`,
      cwd: REPO_ROOT,
      url: `${API_URL}/api/v1/health`,
      timeout: 180_000,
      reuseExistingServer: false,
      env: {
        ...childEnv,
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: API_URL,
        DOTNET_NOLOGO: "1",
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE: "1",
        JobScrape__Storage__DatabasePath: join(ROOT, "jobscrape.db"),
        JobScrape__Storage__ArtifactsRoot: join(ROOT, "artifacts"),
        JobScrape__Storage__ProfilesRoot: join(ROOT, "artifacts", "profiles"),
        JobScrape__Logging__Directory: join(ROOT, "logs"),
        JobScrape__Jwt__Secret: JWT_SECRET,
        JobScrape__AdminSeed__Email: ADMIN_EMAIL,
        JobScrape__AdminSeed__SetupSecret: ADMIN_SEED_SECRET,
      },
    },
    {
      command: "bun run src/server/dev.ts",
      cwd: FRONTEND_ROOT,
      url: `${BASE_URL}/healthz`,
      timeout: 60_000,
      reuseExistingServer: false,
      env: {
        ...childEnv,
        PORT: String(BFF_PORT),
        DOTNET_API_URL: API_URL,
        // Bun auto-loads src/frontend/.env, which points at the local TLS
        // cert. The e2e BFF is plain HTTP, so clear both TLS paths.
        BFF_TLS_CERT_FILE: "",
        BFF_TLS_KEY_FILE: "",
      },
    },
  ],
});
