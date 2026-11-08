import { defineConfig } from "@playwright/test";

/**
 * E2E against the already-running loopback servers: the .NET data plane on :5208 and the Bun BFF on
 * :3000 (see src/frontend/README.md "HTTPS dev loop"). No webServer startup: `bun run test:e2e`
 * never boots or rebuilds anything, it only drives what is already up. One Chromium project using
 * the machine's Google Chrome (`channel: "chrome"`, no `playwright install` download); the BFF
 * serves self-signed HTTPS, so TLS errors are ignored.
 */
export default defineConfig({
  testDir: "./src/tests/e2e",
  testMatch: "**/*.e2e.ts",
  timeout: 30_000,
  retries: 0,
  workers: 1,
  outputDir: "./test-results",
  reporter: "list",
  use: {
    baseURL: "https://127.0.0.1:3000",
    ignoreHTTPSErrors: true,
  },
  projects: [
    {
      name: "chromium",
      use: {
        channel: "chrome",
      },
    },
  ],
});
