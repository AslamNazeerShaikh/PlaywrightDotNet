#!/usr/bin/env bun
/**
 * PR 6 pre-test live validation: boots the real backend on temp ports with a temp database, seeds a
 * disposable admin, and pins every auth/quota/MCP wire behavior the pre-test report calls out. The
 * shared dev backend and its database are never touched; every boot is killed and the temp root is
 * deleted on all exit paths (including SIGINT/SIGTERM). Nonzero exit on any failed check.
 *
 * Groups that would exhaust the 10/min IP login budget are separated by a full host restart, which
 * is also how the quota counter-reset checks work: definitions persist in SQLite, counters live in
 * IMemoryCache and restart empty.
 */

import { createHmac, randomUUID } from "node:crypto";
import { mkdir, mkdtemp, readFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const REPO_ROOT = new URL("../../../../../", import.meta.url).pathname.replace(/\/$/, "");
const API_PROJECT = join(REPO_ROOT, "src/backend/JobScrape.Api/JobScrape.Api.csproj");
const ADMIN_EMAIL = "admin@example.com";
const SEED_SECRET = "Setup-Test-12!!";
const ADMIN_PASSWORD = "Live-Admin-1!!";
const JWT_SECRET = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
const DEMO_PLACEHOLDER = "CHANGE-ME-TO-A-SECRET-OF-AT-LEAST-32-CHARS";
const BOOT_TIMEOUT_MS = 180_000;
const EXIT_TIMEOUT_MS = 60_000;

type Child = ReturnType<typeof Bun.spawn>;
type Json = Record<string, unknown>;

interface Boot {
  base: string;
  child: Child;
  log: string;
}

const children = new Set<Child>();
let tmpRoot: string | null = null;
let dbPath = "";
let current: Boot | null = null;

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
    await rm(tmpRoot, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
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

function checkTrue(name: string, value: boolean, detail = ""): void {
  if (!value) {
    failures.push(`${name}${detail === "" ? "" : `: ${detail}`}`);
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

interface BootOptions {
  seed?: boolean;
  database?: string;
  extraEnv?: Record<string, string>;
}

async function boot(tag: string, options: BootOptions = {}): Promise<Boot> {
  const dir = join(tmpRoot as string, tag);
  await mkdir(dir, { recursive: true });
  const port = await freePort();
  const log = join(dir, "backend.log");
  const seed = options.seed !== false;
  const env: Record<string, string> = {
    ...(process.env as Record<string, string>),
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
    DOTNET_NOLOGO: "1",
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE: "1",
    JobScrape__Storage__DatabasePath: options.database ?? dbPath,
    JobScrape__Storage__ArtifactsRoot: join(tmpRoot as string, "artifacts"),
    JobScrape__Storage__ProfilesRoot: join(tmpRoot as string, "artifacts", "profiles"),
    JobScrape__Logging__Directory: join(tmpRoot as string, "logs", tag),
    JobScrape__Jwt__Secret: JWT_SECRET,
    ...(seed
      ? {
          ADMIN_EMAIL,
          ADMIN_SETUP_SECRET: SEED_SECRET,
          JobScrape__AdminSeed__Email: ADMIN_EMAIL,
          JobScrape__AdminSeed__SetupSecret: SEED_SECRET,
        }
      : {}),
    ...(options.extraEnv ?? {}),
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
      if (response.status === 200) {
        return { base, child, log };
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

async function start(tag: string, options: BootOptions = {}): Promise<Boot> {
  if (current !== null) {
    await kill(current.child);
    current = null;
  }
  current = await boot(tag, options);
  return current;
}

async function stop(): Promise<void> {
  if (current !== null) {
    await kill(current.child);
    current = null;
  }
}

interface Reply {
  status: number;
  headers: Headers;
  json: Json | null;
  text: string;
}

async function call(
  base: string,
  method: string,
  path: string,
  options: {
    token?: string;
    body?: unknown;
    idempotencyKey?: string;
    headers?: Record<string, string>;
  } = {},
): Promise<Reply> {
  const headers: Record<string, string> = { ...(options.headers ?? {}) };
  if (options.token !== undefined) {
    headers["authorization"] = `Bearer ${options.token}`;
  }
  if (options.body !== undefined) {
    headers["content-type"] = "application/json";
  }
  if (options.idempotencyKey !== undefined) {
    headers["idempotency-key"] = options.idempotencyKey;
  }
  const response = await fetch(`${base}${path}`, {
    method,
    headers,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  });
  const text = await response.text();
  let json: Json | null = null;
  try {
    json = text === "" ? null : (JSON.parse(text) as Json);
  } catch {
    json = null;
  }
  return { status: response.status, headers: response.headers, json, text };
}

function code(reply: Reply): unknown {
  return reply.json?.["code"];
}

function bodyOf(reply: Reply): Json {
  if (reply.json === null) {
    throw new Error(`expected a JSON body, got status ${reply.status}: ${reply.text}`);
  }
  return reply.json;
}

function tokenOf(reply: Reply): string {
  const token = bodyOf(reply)["token"];
  if (typeof token !== "string" || token === "") {
    throw new Error(`expected a token, got ${reply.text}`);
  }
  return token;
}

function problemShape(reply: Reply): Json {
  const body = bodyOf(reply);
  return {
    status: body["status"] ?? reply.status,
    code: body["code"] ?? null,
    title: body["title"] ?? null,
    detail: body["detail"] ?? null,
  };
}

function sameProblem(name: string, left: Reply, right: Reply): void {
  const a = problemShape(left);
  const b = problemShape(right);
  check(name, JSON.stringify(a), JSON.stringify(b));
}

function retryAfterSeconds(reply: Reply): number {
  const raw = reply.headers.get("retry-after");
  return raw === null ? Number.NaN : Number(raw);
}

function findUser(reply: Reply, email: string): Json | null {
  const users = bodyOf(reply)["users"];
  if (!Array.isArray(users)) {
    return null;
  }
  return (users as Json[]).find((user) => user["email"] === email) ?? null;
}

function base64Url(value: Buffer): string {
  return value.toString("base64").replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function mintToken(claims: Json): string {
  const now = Math.floor(Date.now() / 1000);
  const header = base64Url(Buffer.from(JSON.stringify({ alg: "HS256", typ: "JWT" })));
  const payload = base64Url(
    Buffer.from(
      JSON.stringify({
        iss: "jobscrape",
        aud: "jobscrape-api",
        nbf: now,
        iat: now,
        exp: now + 3600,
        ...claims,
      }),
    ),
  );
  const signature = createHmac("sha256", JWT_SECRET).update(`${header}.${payload}`).digest();
  return `${header}.${payload}.${base64Url(signature)}`;
}

async function mcpCall(
  base: string,
  token: string | undefined,
  tool: string,
  args: Json,
): Promise<Reply> {
  return call(base, "POST", "/mcp", {
    token,
    headers: { accept: "application/json, text/event-stream" },
    body: {
      jsonrpc: "2.0",
      id: randomUUID(),
      method: "tools/call",
      params: { name: tool, arguments: args },
    },
  });
}

/** Extracts the JSON-RPC result from a JSON or SSE response body. */
function mcpMessage(reply: Reply): Json | null {
  const candidates: string[] = [];
  if (reply.json !== null) {
    candidates.push(reply.text);
  } else {
    for (const line of reply.text.split("\n")) {
      if (line.startsWith("data:")) {
        candidates.push(line.slice("data:".length).trim());
      }
    }
  }
  for (const candidate of candidates) {
    try {
      const parsed = JSON.parse(candidate) as Json;
      if (parsed["result"] !== undefined || parsed["error"] !== undefined) {
        return parsed;
      }
    } catch {
      // Not the payload line.
    }
  }
  return null;
}

function mcpIsToolDenied(reply: Reply): boolean {
  const message = mcpMessage(reply);
  const result = message?.["result"] as Json | undefined;
  if (result === undefined || result["isError"] !== true) {
    return false;
  }
  const content = result["content"];
  return JSON.stringify(content ?? []).includes("ACCESS_DENIED");
}

function mcpIsToolAllowed(reply: Reply): boolean {
  const message = mcpMessage(reply);
  const result = message?.["result"] as Json | undefined;
  return message?.["error"] === undefined && result !== undefined && result["isError"] !== true;
}

async function expectBootFailure(tag: string, extraEnv: Record<string, string>): Promise<void> {
  const dir = join(tmpRoot as string, tag);
  await mkdir(dir, { recursive: true });
  const port = await freePort();
  const log = join(dir, "backend.log");
  const child = track(
    Bun.spawn(["dotnet", "run", "--project", API_PROJECT, "--no-launch-profile"], {
      cwd: REPO_ROOT,
      env: {
        ...(process.env as Record<string, string>),
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
        DOTNET_NOLOGO: "1",
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE: "1",
        JobScrape__Storage__DatabasePath: join(dir, "jobscrape.db"),
        JobScrape__Storage__ArtifactsRoot: join(dir, "artifacts"),
        JobScrape__Storage__ProfilesRoot: join(dir, "artifacts", "profiles"),
        JobScrape__Logging__Directory: join(dir, "logs"),
        JobScrape__Jwt__Secret: JWT_SECRET,
        ...extraEnv,
      },
      stdout: Bun.file(log),
      stderr: Bun.file(log),
    }),
  );
  const deadline = Date.now() + EXIT_TIMEOUT_MS;
  while (child.exitCode === null && Date.now() < deadline) {
    await Bun.sleep(250);
  }
  if (child.exitCode === null) {
    await kill(child);
    failures.push(`${tag}: host stayed up despite an unsafe configuration`);
    return;
  }
  checkTrue(`${tag}: startup exits nonzero`, child.exitCode !== 0, `exit ${child.exitCode}`);
  children.delete(child);
}

async function main(): Promise<number> {
  tmpRoot = await mkdtemp(join(tmpdir(), "jobscrape-pretest-"));
  dbPath = join(tmpRoot, "jobscrape.db");

  try {
    // Group 1: fresh boot, seed, login oracle, setup matrix.
    let app = await start("seed");
    let base = app.base;

    const setup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: { email: ADMIN_EMAIL, currentSecret: SEED_SECRET, newPassword: ADMIN_PASSWORD },
    });
    check("fresh boot: admin setup 200", setup.status, 200);
    check("fresh boot: setup mustChangePassword false", bodyOf(setup)["mustChangePassword"], false);
    let adminToken = tokenOf(setup);

    const quotas = await call(base, "GET", "/api/v1/quotas", { token: adminToken });
    check("fresh boot: quotas 200", quotas.status, 200);
    const quotaRows = bodyOf(quotas)["quotas"] as Json[];
    check("fresh boot: two quota scopes", quotaRows.length, 2);
    const apiQuota = quotaRows.find((row) => row["scope"] === "Api");
    const mcpQuota = quotaRows.find((row) => row["scope"] === "Mcp");
    check("fresh boot: Api limit", apiQuota?.["limit"], 10000);
    check("fresh boot: Api window", apiQuota?.["window"], "1.00:00:00");
    check("fresh boot: Mcp limit", mcpQuota?.["limit"], 1000);

    const roles = await call(base, "GET", "/api/v1/roles", { token: adminToken });
    const roleRows = bodyOf(roles)["roles"] as Json[];
    const roleNames = roleRows.map((row) => row["name"]).sort();
    check(
      "fresh boot: three seeded roles",
      JSON.stringify(roleNames),
      '["Admin","Operator","Viewer"]',
    );

    const pendingEmail = `pending-${randomUUID()}@example.com`;
    const pendingCreate = await call(base, "POST", "/api/v1/users", {
      token: adminToken,
      body: { email: pendingEmail, temporaryPassword: "Temp-Secret1!!" },
    });
    check("create user 201", pendingCreate.status, 201);
    const usersAfterCreate = await call(base, "GET", "/api/v1/users", { token: adminToken });
    const pendingUser = findUser(usersAfterCreate, pendingEmail);
    checkTrue("new user exists", pendingUser !== null);
    checkTrue(
      "new user auto-assigned Viewer",
      Array.isArray(pendingUser?.["roles"]) &&
        (pendingUser?.["roles"] as string[]).includes("Viewer"),
    );

    const badPassword = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: ADMIN_EMAIL, password: "Wrong-Password1!!" },
    });
    check("login wrong password 401", badPassword.status, 401);
    check("login wrong password code", code(badPassword), "INVALID_CREDENTIALS");

    const unknownLogin = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: `ghost-${randomUUID()}@example.com`, password: "Wrong-Password1!!" },
    });
    check("login unknown email 401", unknownLogin.status, 401);
    check("login unknown email code", code(unknownLogin), "INVALID_CREDENTIALS");

    const pendingLogin = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: pendingEmail, password: "Temp-Secret1!!" },
    });
    check("login pending temp secret 401", pendingLogin.status, 401);
    check("login pending temp secret code", code(pendingLogin), "INVALID_CREDENTIALS");
    sameProblem("login oracle: unknown vs pending bodies identical", unknownLogin, pendingLogin);
    sameProblem(
      "login oracle: unknown vs wrong-password bodies identical",
      unknownLogin,
      badPassword,
    );

    const unknownSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: {
        email: `ghost-${randomUUID()}@example.com`,
        currentSecret: "whatever",
        newPassword: "New-Password3##",
      },
    });
    check("setup unknown email 401", unknownSetup.status, 401);
    check("setup unknown email code", code(unknownSetup), "INVALID_CREDENTIALS");

    const wrongSecretSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: { email: pendingEmail, currentSecret: "wrong-secret", newPassword: "New-Password3##" },
    });
    check("setup pending wrong secret 401", wrongSecretSetup.status, 401);
    check("setup pending wrong secret code", code(wrongSecretSetup), "INVALID_CREDENTIALS");
    sameProblem(
      "setup oracle: unknown vs wrong-secret bodies identical",
      unknownSetup,
      wrongSecretSetup,
    );

    const weakSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: { email: pendingEmail, currentSecret: "Temp-Secret1!!", newPassword: "short" },
    });
    check("setup weak password 400", weakSetup.status, 400);
    check("setup weak password code", code(weakSetup), "VALIDATION_FAILED");
    const weakErrors = bodyOf(weakSetup)["errors"] as Json | undefined;
    checkTrue(
      "setup weak password: errors.newPassword",
      weakErrors !== undefined && Array.isArray(weakErrors["newPassword"]),
    );

    const pendingSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: {
        email: pendingEmail,
        currentSecret: "Temp-Secret1!!",
        newPassword: "Pending-Pass9!!",
      },
    });
    check("setup correct 200", pendingSetup.status, 200);
    check(
      "setup correct mustChangePassword false",
      bodyOf(pendingSetup)["mustChangePassword"],
      false,
    );

    const doubleSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: {
        email: pendingEmail,
        currentSecret: "Pending-Pass9!!",
        newPassword: "Pending-Pass9!!",
      },
    });
    check("double setup 409", doubleSetup.status, 409);
    check("double setup code", code(doubleSetup), "PASSWORD_SETUP_COMPLETE");

    // Group 2: lockout. A restart resets the shared 10/min auth IP budget.
    app = await start("lockout");
    base = app.base;
    const lockEmail = `lock-${randomUUID()}@example.com`;
    const lockCreate = await call(base, "POST", "/api/v1/users", {
      token: adminToken,
      body: { email: lockEmail, temporaryPassword: "Temp-Secret1!!" },
    });
    check("lockout user created", lockCreate.status, 201);
    const lockSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: { email: lockEmail, currentSecret: "Temp-Secret1!!", newPassword: "Locked-Pass2@@" },
    });
    check("lockout user setup 200", lockSetup.status, 200);
    const lockLogin = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: lockEmail, password: "Locked-Pass2@@" },
    });
    check("lockout user login 200", lockLogin.status, 200);
    const lockToken = tokenOf(lockLogin);

    for (let attempt = 1; attempt <= 5; attempt++) {
      const failed = await call(base, "POST", "/api/v1/auth/login", {
        body: { email: lockEmail, password: "Wrong-Password1!!" },
      });
      check(`lockout fail ${attempt} 401`, failed.status, 401);
      check(`lockout fail ${attempt} code`, code(failed), "INVALID_CREDENTIALS");
    }

    const locked = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: lockEmail, password: "Locked-Pass2@@" },
    });
    check("locked correct password 403", locked.status, 403);
    check("locked code", code(locked), "ACCOUNT_LOCKED");

    const revoked = await call(base, "GET", "/api/v1/auth/me", { token: lockToken });
    check("lockout old token 401", revoked.status, 401);
    check("lockout old token code", code(revoked), "SESSION_REVOKED");

    // Group 3: role changes, self-edit guard, token-version sign-out.
    app = await start("roles");
    base = app.base;
    const secondEmail = `second-${randomUUID()}@example.com`;
    const secondCreate = await call(base, "POST", "/api/v1/users", {
      token: adminToken,
      body: { email: secondEmail, temporaryPassword: "Temp-Secret1!!" },
    });
    check("second admin target created", secondCreate.status, 201);
    const secondId = bodyOf(secondCreate)["id"] as string;
    const secondSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: { email: secondEmail, currentSecret: "Temp-Secret1!!", newPassword: "Second-Pass2@@" },
    });
    check("second admin target setup 200", secondSetup.status, 200);
    let secondToken = tokenOf(secondSetup);

    const grantOperator = await call(base, "POST", `/api/v1/users/${secondId}/roles`, {
      token: adminToken,
      body: { role: "Operator" },
    });
    check("grant Operator 200", grantOperator.status, 200);
    const secondStale = await call(base, "GET", "/api/v1/auth/me", { token: secondToken });
    check("grant revokes old token 401", secondStale.status, 401);
    check("grant revokes old token code", code(secondStale), "SESSION_REVOKED");

    const secondRelogin = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: secondEmail, password: "Second-Pass2@@" },
    });
    check("second re-login 200", secondRelogin.status, 200);
    secondToken = tokenOf(secondRelogin);
    const secondMe = await call(base, "GET", "/api/v1/auth/me", { token: secondToken });
    const secondRoles = bodyOf(secondMe)["roles"] as string[];
    checkTrue(
      "second holds Viewer and Operator",
      ["Viewer", "Operator"].every((r) => secondRoles.includes(r)),
    );

    const adminId = bodyOf(await call(base, "GET", "/api/v1/auth/me", { token: adminToken }))[
      "email"
    ] as string;
    check("admin me email", adminId, ADMIN_EMAIL);
    const adminUserId = findUser(
      await call(base, "GET", "/api/v1/users", { token: adminToken }),
      ADMIN_EMAIL,
    )?.["id"] as string;
    const selfAssign = await call(base, "POST", `/api/v1/users/${adminUserId}/roles`, {
      token: adminToken,
      body: { role: "Admin" },
    });
    check("self role assign 403", selfAssign.status, 403);
    check("self role assign code", code(selfAssign), "ACCESS_DENIED");
    const selfRevoke = await call(base, "DELETE", `/api/v1/users/${adminUserId}/roles/Admin`, {
      token: adminToken,
    });
    check("self role revoke 403", selfRevoke.status, 403);
    check("self role revoke code", code(selfRevoke), "ACCESS_DENIED");

    const grantAdmin = await call(base, "POST", `/api/v1/users/${secondId}/roles`, {
      token: adminToken,
      body: { role: "Admin" },
    });
    check("grant Admin to second 200", grantAdmin.status, 200);
    const secondAdminLogin = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: secondEmail, password: "Second-Pass2@@" },
    });
    check("second admin login 200", secondAdminLogin.status, 200);
    secondToken = tokenOf(secondAdminLogin);

    const secondRevokesAdmin = await call(
      base,
      "DELETE",
      `/api/v1/users/${adminUserId}/roles/Admin`,
      { token: secondToken },
    );
    check("second admin revokes first admin 200", secondRevokesAdmin.status, 200);
    const firstStale = await call(base, "GET", "/api/v1/auth/me", { token: adminToken });
    check("revoked admin old token 401", firstStale.status, 401);
    check("revoked admin old token code", code(firstStale), "SESSION_REVOKED");
    const firstRelogin = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: ADMIN_EMAIL, password: ADMIN_PASSWORD },
    });
    check("revoked admin re-login 200", firstRelogin.status, 200);
    adminToken = tokenOf(firstRelogin);
    const firstMe = await call(base, "GET", "/api/v1/auth/me", { token: adminToken });
    check("revoked admin has no Admin role", JSON.stringify(bodyOf(firstMe)["roles"]), "[]");

    const soleAdminSelfRevoke = await call(
      base,
      "DELETE",
      `/api/v1/users/${secondId}/roles/Admin`,
      {
        token: secondToken,
      },
    );
    check("sole admin self-revoke 403", soleAdminSelfRevoke.status, 403);
    check("sole admin self-revoke code", code(soleAdminSelfRevoke), "ACCESS_DENIED");
    // LAST_ADMIN_REQUIRED (409) is handler-pinned only; the self-edit guard
    // fires first on every deterministic single-host path (see IdentityHandlerTests).
    console.log("note - LAST_ADMIN_REQUIRED 409 is not HTTP-reachable post self-edit guard");

    // Group 4: rate limiting. /me must not consume the login budget.
    app = await start("rate-limit");
    base = app.base;
    for (let attempt = 1; attempt <= 11; attempt++) {
      const me = await call(base, "GET", "/api/v1/auth/me", { token: secondToken });
      check(`me poll ${attempt} 200`, me.status, 200);
    }
    const loginAfterMe = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: secondEmail, password: "Second-Pass2@@" },
    });
    check("login after 11 me calls 200", loginAfterMe.status, 200);

    let firstLimited = -1;
    let limited: Reply | null = null;
    for (let attempt = 0; attempt < 12; attempt++) {
      const spam = await call(base, "POST", "/api/v1/auth/login", {
        body: { email: `spam-${randomUUID()}@example.com`, password: "Wrong-Password1!!" },
      });
      if (spam.status === 429) {
        firstLimited = attempt + 1;
        limited = spam;
        break;
      }
      check(`login spam ${attempt + 1} 401`, spam.status, 401);
    }
    check("first 429 is the 10th spam after one login already spent", firstLimited, 10);
    check("rate limit status 429", limited?.status, 429);
    check("rate limit code", limited === null ? null : code(limited), "RATE_LIMITED");
    check("rate limit Retry-After", limited === null ? null : retryAfterSeconds(limited), 60);
    checkTrue(
      "rate limit problem+json",
      limited?.headers.get("content-type")?.startsWith("application/problem+json") === true,
    );
    const rotatedEmail = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: `rotated-${randomUUID()}@example.com`, password: "Wrong-Password1!!" },
    });
    check("rotating emails cannot bypass the IP cap", rotatedEmail.status, 429);

    // Group 5: idempotency replay, reuse, and auth exemption.
    app = await start("idempotency");
    base = app.base;
    const idemKey = randomUUID();
    const idemEmail = `idem-${randomUUID()}@example.com`;
    const idemBody = { email: idemEmail, temporaryPassword: "Temp-Secret1!!" };
    const firstPost = await call(base, "POST", "/api/v1/users", {
      token: secondToken,
      body: idemBody,
      idempotencyKey: idemKey,
    });
    check("idempotent first post 201", firstPost.status, 201);
    const replay = await call(base, "POST", "/api/v1/users", {
      token: secondToken,
      body: idemBody,
      idempotencyKey: idemKey,
    });
    check("idempotent replay 201", replay.status, 201);
    check("idempotent replay byte-identical", replay.text, firstPost.text);

    const reused = await call(base, "POST", "/api/v1/users", {
      token: secondToken,
      body: { email: `other-${randomUUID()}@example.com`, temporaryPassword: "Temp-Secret1!!" },
      idempotencyKey: idemKey,
    });
    check("idempotency key reused 409", reused.status, 409);
    check("idempotency key reused code", code(reused), "IDEMPOTENCY_KEY_REUSED");
    const usersAfterReuse = await call(base, "GET", "/api/v1/users", { token: secondToken });
    checkTrue(
      "reused body never executed",
      findUser(usersAfterReuse, idemEmail) !== null &&
        (bodyOf(usersAfterReuse)["users"] as Json[]).every(
          (user) => !String(user["email"]).startsWith("other-"),
        ),
    );

    const loginKey = randomUUID();
    const loginReplay1 = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: secondEmail, password: "Second-Pass2@@" },
      idempotencyKey: loginKey,
    });
    const loginReplay2 = await call(base, "POST", "/api/v1/auth/login", {
      body: { email: secondEmail, password: "Second-Pass2@@" },
      idempotencyKey: loginKey,
    });
    check("same-key login first 200", loginReplay1.status, 200);
    check("same-key login second 200", loginReplay2.status, 200);
    checkTrue(
      "login responses are never replayed",
      tokenOf(loginReplay1) !== tokenOf(loginReplay2),
    );

    // Group 6: quotas, restart reset, exempt reads, MCP scope.
    app = await start("quota-setup");
    base = app.base;
    const lowerApi = await call(base, "PUT", "/api/v1/quotas/Api", {
      token: secondToken,
      body: { limit: 1, window: "00:01:00" },
    });
    check("lower Api quota 200", lowerApi.status, 200);

    app = await start("quota-restart-1");
    base = app.base;
    const firstApiRead = await call(base, "GET", "/api/v1/users", { token: secondToken });
    check("api quota first read after restart 200", firstApiRead.status, 200);
    const secondApiRead = await call(base, "GET", "/api/v1/users", { token: secondToken });
    check("api quota second read 429", secondApiRead.status, 429);
    check("api quota code", code(secondApiRead), "QUOTA_EXCEEDED");
    checkTrue("api quota Retry-After present", retryAfterSeconds(secondApiRead) > 0);
    const quotasExempt1 = await call(base, "GET", "/api/v1/quotas", { token: secondToken });
    const quotasExempt2 = await call(base, "GET", "/api/v1/quotas", { token: secondToken });
    check("quota reads exempt first 200", quotasExempt1.status, 200);
    check("quota reads exempt second 200", quotasExempt2.status, 200);
    const persistedApi = (bodyOf(quotasExempt1)["quotas"] as Json[]).find(
      (row) => row["scope"] === "Api",
    );
    check("api quota definition persisted across restart", persistedApi?.["limit"], 1);

    app = await start("quota-restart-2");
    base = app.base;
    const resetRead = await call(base, "GET", "/api/v1/users", { token: secondToken });
    check("api quota counter reset after second restart 200", resetRead.status, 200);

    app = await start("quota-restore");
    base = app.base;
    const restoreApi = await call(base, "PUT", "/api/v1/quotas/Api", {
      token: secondToken,
      body: { limit: 10000, window: "1.00:00:00" },
    });
    check("restore Api quota 200", restoreApi.status, 200);
    const restoredQuotas = await call(base, "GET", "/api/v1/quotas", { token: secondToken });
    check(
      "Api quota restored",
      (bodyOf(restoredQuotas)["quotas"] as Json[]).find((row) => row["scope"] === "Api")?.["limit"],
      10000,
    );

    const lowerMcp = await call(base, "PUT", "/api/v1/quotas/Mcp", {
      token: secondToken,
      body: { limit: 1, window: "00:01:00" },
    });
    check("lower Mcp quota 200", lowerMcp.status, 200);
    const mcpFirst = await call(base, "POST", "/mcp", {
      token: secondToken,
      headers: { accept: "application/json, text/event-stream" },
      body: {
        jsonrpc: "2.0",
        id: 1,
        method: "initialize",
        params: {
          protocolVersion: "2025-06-18",
          capabilities: {},
          clientInfo: { name: "pretest", version: "1.0.0" },
        },
      },
    });
    checkTrue("mcp first call not 429", mcpFirst.status !== 429, `status ${mcpFirst.status}`);
    const mcpSecond = await call(base, "POST", "/mcp", {
      token: secondToken,
      headers: { accept: "application/json, text/event-stream" },
      body: {
        jsonrpc: "2.0",
        id: 2,
        method: "initialize",
        params: {
          protocolVersion: "2025-06-18",
          capabilities: {},
          clientInfo: { name: "pretest", version: "1.0.0" },
        },
      },
    });
    check("mcp second call 429", mcpSecond.status, 429);
    check("mcp quota code", code(mcpSecond), "QUOTA_EXCEEDED");
    checkTrue("mcp quota Retry-After present", retryAfterSeconds(mcpSecond) > 0);

    app = await start("mcp-restart");
    base = app.base;
    const mcpAfterRestart = await call(base, "POST", "/mcp", {
      token: secondToken,
      headers: { accept: "application/json, text/event-stream" },
      body: {
        jsonrpc: "2.0",
        id: 3,
        method: "initialize",
        params: {
          protocolVersion: "2025-06-18",
          capabilities: {},
          clientInfo: { name: "pretest", version: "1.0.0" },
        },
      },
    });
    checkTrue("mcp counter reset after restart", mcpAfterRestart.status !== 429);
    const restoreMcp = await call(base, "PUT", "/api/v1/quotas/Mcp", {
      token: secondToken,
      body: { limit: 1000, window: "1.00:00:00" },
    });
    check("restore Mcp quota 200", restoreMcp.status, 200);

    // Group 7: MCP auth. Pending tokens are not login-issuable by design, so
    // mint one with the run's dev secret against the persisted pending user.
    const pendingMcpEmail = `mcp-pending-${randomUUID()}@example.com`;
    const pendingMcpCreate = await call(base, "POST", "/api/v1/users", {
      token: secondToken,
      body: { email: pendingMcpEmail, temporaryPassword: "Temp-Secret1!!" },
    });
    check("mcp pending user created", pendingMcpCreate.status, 201);
    const pendingMcpId = bodyOf(pendingMcpCreate)["id"] as string;
    const pendingToken = mintToken({
      sub: pendingMcpId,
      email: pendingMcpEmail,
      role: ["Viewer"],
      perm: ["profiles.read", "runs.read"],
      jti: randomUUID(),
      ver: "0",
      must_change_password: "true",
    });

    const anonymousMcp = await mcpCall(base, undefined, "list_runs", {});
    check("mcp anonymous 401", anonymousMcp.status, 401);
    check("mcp anonymous code", code(anonymousMcp), "UNAUTHENTICATED");

    const pendingMcp = await mcpCall(base, pendingToken, "list_runs", {});
    check("mcp pending tool call HTTP 200", pendingMcp.status, 200);
    checkTrue("mcp pending tool call denied", mcpIsToolDenied(pendingMcp));

    const viewerSetup = await call(base, "POST", "/api/v1/auth/setup-password", {
      body: {
        email: pendingMcpEmail,
        currentSecret: "Temp-Secret1!!",
        newPassword: "Viewer-Pass3##",
      },
    });
    check("mcp viewer setup 200", viewerSetup.status, 200);
    const viewerToken = tokenOf(viewerSetup);
    const noPerm = await mcpCall(base, viewerToken, "list_sources", {});
    check("mcp no-perm tool HTTP 200", noPerm.status, 200);
    checkTrue("mcp no-perm tool denied", mcpIsToolDenied(noPerm));
    const allowed = await mcpCall(base, viewerToken, "list_runs", {});
    checkTrue("mcp permitted tool allowed", mcpIsToolAllowed(allowed));

    // Group 8: startup configuration. The shipped appsettings seeds the
    // default admin with no environment override (fresh-boot item), while the
    // demo placeholder secret still fails startup.
    await start("default-seed", {
      seed: false,
      database: join(tmpRoot as string, "default-seed.db"),
    });
    const defaultBase = current!.base;
    const defaultSetup = await call(defaultBase, "POST", "/api/v1/auth/setup-password", {
      body: { email: ADMIN_EMAIL, currentSecret: SEED_SECRET, newPassword: "Default-Pass4$$" },
    });
    check("fresh boot without admin env uses shipped seed 200", defaultSetup.status, 200);

    await expectBootFailure("placeholder-secret", {
      JobScrape__Jwt__Secret: DEMO_PLACEHOLDER,
    });
  } catch (error) {
    failures.push(error instanceof Error ? error.message : String(error));
    if (tmpRoot !== null) {
      const { readdir } = await import("node:fs/promises");
      try {
        for (const entry of await readdir(tmpRoot)) {
          try {
            const log = join(tmpRoot, entry, "backend.log");
            const lines = (await readFile(log, "utf8")).trimEnd().split("\n");
            console.error(`--- ${entry}/backend.log (last 40 lines) ---`);
            for (const line of lines.slice(-40)) {
              console.error(line);
            }
          } catch {
            // No log for this entry.
          }
        }
      } catch {
        // The temp root may not exist yet.
      }
    }
  } finally {
    await stop();
    await cleanup();
  }

  if (failures.length > 0) {
    for (const failure of failures) {
      console.error(`not ok - ${failure}`);
    }
    return 1;
  }
  console.log("pre-test live validation passed");
  return 0;
}

process.exit(await main());
