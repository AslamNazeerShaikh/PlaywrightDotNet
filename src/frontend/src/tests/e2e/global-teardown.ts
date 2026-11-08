import { rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

/**
 * Removes the temp backend root (SQLite file, artifacts, logs) created for this run. Playwright
 * stops the webServers before teardown; retries absorb a brief SQLite/WAL handle release. The root
 * is fixed by default so this process does not depend on inherited config state.
 */
export default async function globalTeardown(): Promise<void> {
  const root = process.env.JOBSCRAPE_E2E_ROOT ?? join(tmpdir(), "jobscrape-e2e");
  await rm(root, { recursive: true, force: true, maxRetries: 10, retryDelay: 200 });
}
