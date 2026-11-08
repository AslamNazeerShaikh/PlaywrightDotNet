#!/usr/bin/env bun
/**
 * Flag-off validation: boots the real backend twice on temp ports with temp databases, then pins
 * the flag-off HTTP contract. No manual restarts, no shared-state writes, no stray processes: every
 * boot is killed and its temp dir removed on all exit paths (including SIGINT/SIGTERM).
 *
 * Boot 1 (default flags) proves the harness: /api/v1/health answers 200. Boot 2 (Health=false,
 * CsvExport=false) asserts:
 *
 * - Features payload reports health/csvExport false, the rest true;
 * - Health answers 404 FEATURE_DISABLED;
 * - Export?format=csv answers 404 FEATURE_DISABLED (gate runs before the store read, so the run id
 *   need not exist);
 * - Export?format=xlsx answers 404 RUN_NOT_FOUND: the flag gate and the writer lookup both pass and
 *   only the store read misses. A literal 200 needs a completed browser-driven run, which is
 *   nondeterministic in a script, so RUN_NOT_FOUND is the deterministic writer-present proof.
 */

import { randomUUID } from "node:crypto";
import { mkdir, mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const REPO_ROOT = new URL("../../../../..", import.meta.url).pathname.replace(/\/$/, "");
const API_PROJECT = join(REPO_ROOT, "src/backend/JobScrape.Api/JobScrape.Api.csproj");
const ADMIN_EMAIL = "admin@example.com";
const SEED_SECRET = "Setup-Test-12!!";
const NEW_PASSWORD = "Validate-Flags-42!!";
const BOOT_TIMEOUT_MS = 180_000;

type Child = ReturnType<typeof Bun.spawn>;

const children = new Set<Child>();
let tmpRoot: string | null = null;

function track(child: Child): Child {
  children.add(child);
  return child;
}

async function kill(child: Child): Promise<void> {
  children.delete(child);
  try {
    child.kill("SIGTERM");
    await Promise.race([child.exited, new Promise((resolve) => setTimeout(resolve, 10_000))]);
  } catch {
    // Already gone; teardown continues.
  }
  try {
    if (child.exitCode === null) {
      child.kill("SIGKILL");
      await child.exited;
    }
  } catch {
    // Already gone; teardown continues.
  }
}

async function cleanup(): Promise<void> {
  for (const child of [...children]) {
    await kill(child);
  }
  if (tmpRoot !== null) {
    await rm(tmpRoot, { recursive: true, force: true });
    tmpRoot = null;
  }
}

process.on("SIGINT", () => void cleanup().finally(() => process.exit(130)));
process.on("SIGTERM", () => void cleanup().finally(() => process.exit(143)));

const failures: string[] = [];

function check(name: string, actual: unknown, expected: unknown): void {
  if (actual !== expected) {
    failures.push(`${name}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  } else {
    console.log(`ok - ${name}`);
  }
}

async function freePort(): Promise<number> {
  const probe = Bun.serve({ port: 0, fetch: () => new Response("probe") });
  const port = probe.port;
  probe.stop(true);
  if (port === undefined) {
    throw new Error("could not acquire a free loopback port");
  }
  return port;
}

async function boot(
  extraEnv: Record<string, string>,
  tag: string,
): Promise<{ base: string; log: string }> {
  const dir = join(tmpRoot as string, tag);
  await mkdir(dir, { recursive: true });
  const port = await freePort();
  const log = join(dir, "backend.log");
  const env = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    DOTNET_NOLOGO: "1",
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE: "1",
    JobScrape__Storage__DatabasePath: join(dir, "jobscrape.db"),
    JobScrape__Storage__ArtifactsRoot: join(dir, "artifacts"),
    JobScrape__Storage__ProfilesRoot: join(dir, "artifacts", "profiles"),
    JobScrape__Logging__Directory: join(dir, "logs"),
    ...extraEnv,
  };
  const child = track(
    Bun.spawn(["dotnet", "run", "--project", API_PROJECT, "--no-launch-profile"], {
      cwd: REPO_ROOT,
      env,
      stdout: Bun.file(log),
      stderr: Bun.file(log),
    }),
  );
  const base = `http://127.0.0.1:${port}`;
  const deadline = Date.now() + BOOT_TIMEOUT_MS;
  for (;;) {
    if (child.exitCode !== null) {
      throw new Error(`${tag} backend exited during boot (code ${child.exitCode}); see ${log}`);
    }
    try {
      const response = await fetch(`${base}/api/v1/health`);
      if (response.status === 200 || response.status === 404) {
        return { base, log };
      }
    } catch {
      // Not listening yet.
    }
    if (Date.now() > deadline) {
      throw new Error(
        `${tag} backend did not answer within ${BOOT_TIMEOUT_MS / 1000}s; see ${log}`,
      );
    }
    await Bun.sleep(1000);
  }
}

async function json(response: Response): Promise<Record<string, unknown>> {
  return (await response.json()) as Record<string, unknown>;
}

async function main(): Promise<number> {
  tmpRoot = await mkdtemp(join(tmpdir(), "jobscrape-flags-"));
  try {
    // Boot 1: default flags prove the harness works end to end.
    const main = await boot({}, "main");
    try {
      const health = await fetch(`${main.base}/api/v1/health`);
      check("main boot health status", health.status, 200);
      const features = await json(await fetch(`${main.base}/api/v1/features`));
      for (const flag of ["authentication", "logging", "health", "csvExport", "excelExport"]) {
        check(`main boot features.${flag}`, features[flag], true);
      }
    } finally {
      for (const child of [...children]) {
        await kill(child);
      }
    }

    // Boot 2: health and CSV export off.
    const flagged = await boot(
      { JobScrape__Features__Health: "false", JobScrape__Features__CsvExport: "false" },
      "flagged",
    );
    try {
      const features = await json(await fetch(`${flagged.base}/api/v1/features`));
      check("flag-off features.health", features["health"], false);
      check("flag-off features.csvExport", features["csvExport"], false);
      check("flag-off features.authentication", features["authentication"], true);
      check("flag-off features.logging", features["logging"], true);
      check("flag-off features.excelExport", features["excelExport"], true);

      const health = await fetch(`${flagged.base}/api/v1/health`);
      check("flag-off health status", health.status, 404);
      check("flag-off health code", (await json(health))["code"], "FEATURE_DISABLED");

      const setup = await fetch(`${flagged.base}/api/v1/auth/setup-password`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          email: ADMIN_EMAIL,
          currentSecret: SEED_SECRET,
          newPassword: NEW_PASSWORD,
        }),
      });
      check("flag-off setup-password status", setup.status, 200);

      const login = await fetch(`${flagged.base}/api/v1/auth/login`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ email: ADMIN_EMAIL, password: NEW_PASSWORD }),
      });
      check("flag-off login status", login.status, 200);
      const token = (await json(login))["token"];
      check("flag-off login token present", typeof token, "string");
      const auth = { authorization: `Bearer ${String(token)}` };
      const missing = randomUUID();

      const csv = await fetch(`${flagged.base}/api/v1/runs/${missing}/postings/export?format=csv`, {
        headers: auth,
      });
      check("flag-off csv export status", csv.status, 404);
      check("flag-off csv export code", (await json(csv))["code"], "FEATURE_DISABLED");

      const xlsx = await fetch(
        `${flagged.base}/api/v1/runs/${missing}/postings/export?format=xlsx`,
        { headers: auth },
      );
      check("flag-off xlsx export status", xlsx.status, 404);
      check("flag-off xlsx export code", (await json(xlsx))["code"], "RUN_NOT_FOUND");
    } finally {
      for (const child of [...children]) {
        await kill(child);
      }
    }
  } catch (error) {
    failures.push(error instanceof Error ? error.message : String(error));
    if (tmpRoot !== null) {
      const { readFile } = await import("node:fs/promises");
      for (const tag of ["main", "flagged"]) {
        try {
          const log = join(tmpRoot, tag, "backend.log");
          const lines = (await readFile(log, "utf8")).trimEnd().split("\n");
          console.error(`--- ${tag}/backend.log (last 40 lines) ---`);
          for (const line of lines.slice(-40)) {
            console.error(line);
          }
        } catch {
          // No log yet; the failures list already says why.
        }
      }
    }
  } finally {
    await cleanup();
  }

  if (failures.length > 0) {
    for (const failure of failures) {
      console.error(`not ok - ${failure}`);
    }
    return 1;
  }
  console.log("flag-off validation passed");
  return 0;
}

process.exit(await main());
