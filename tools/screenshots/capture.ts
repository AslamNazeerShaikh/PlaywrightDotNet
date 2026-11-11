/**
 * Full-page light plus dark captures of every ops-deck route over CDP.
 *
 * Run: `bun tools/screenshots/capture.ts` from the repo root.
 * Env: `BFF_URL` (default `https://127.0.0.1:3000`),
 * `SHOT_OUT` (default `docs/screenshots`), `CDP_PORT` (default `9222`),
 * `PLAYWRIGHT_BROWSERS_PATH` (default: the OS ms-playwright cache).
 *
 * Launches the Playwright bundled Chromium itself with remote debugging,
 * then shuts it down. No system browser is used. Needs a bundled Chromium
 * install (docs/09-operations-runbook.md §0).
 *
 * Theme uses the deck THEME toggle with a confirm poll. A fixed sleep raced
 * React state and produced light files named dark, so the poll is load-bearing.
 */
import { existsSync, mkdtempSync, readdirSync, rmSync } from "node:fs";
import { homedir, tmpdir } from "node:os";
import { join } from "node:path";

const BFF = process.env["BFF_URL"] ?? "https://127.0.0.1:3000";
const OUT = process.env["SHOT_OUT"] ?? "docs/screenshots";
const CDP = process.env["CDP_PORT"] ?? "9222";
const ADMIN_EMAIL = process.env["ADMIN_EMAIL"] ?? "admin@example.com";
const ADMIN_PASSWORD = process.env["ADMIN_PASSWORD"] ?? "AslamAslam@123";
const routes = [
  ["01-overview", "overview"],
  ["02-health", "health"],
  ["03-profiles", "profiles"],
  ["04-sources", "sources"],
  ["05-sessions", "sessions"],
  ["06-runs", "runs"],
  ["07-topology", "topology"],
  ["08-database", "database"],
  ["09-users", "users"],
  ["10-roles", "roles"],
  ["11-quotas", "quotas"],
];

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms));

function browsersRoot(): string {
  if (process.env["PLAYWRIGHT_BROWSERS_PATH"])
    return process.env["PLAYWRIGHT_BROWSERS_PATH"];
  switch (process.platform) {
    case "darwin":
      return join(homedir(), "Library", "Caches", "ms-playwright");
    case "win32":
      return join(
        process.env["LOCALAPPDATA"] ?? join(homedir(), "AppData", "Local"),
        "ms-playwright",
      );
    default:
      return join(homedir(), ".cache", "ms-playwright");
  }
}

function bundledChromium(): string {
  const root = browsersRoot();
  let entries: string[] = [];
  try {
    entries = readdirSync(root);
  } catch {
    // Missing cache dir; handled by the throw below.
  }
  const revisions = entries
    .map((d) => /^chromium-(\d+)$/.exec(d)?.[1])
    .filter((v): v is string => v !== undefined)
    .map(Number)
    .sort((a, b) => b - a);
  for (const rev of revisions) {
    const dir = join(root, `chromium-${rev}`);
    const candidate =
      process.platform === "darwin"
        ? join(
            dir,
            `chrome-mac-${process.arch === "arm64" ? "arm64" : "x64"}`,
            "Google Chrome for Testing.app",
            "Contents",
            "MacOS",
            "Google Chrome for Testing",
          )
        : process.platform === "win32"
          ? [join(dir, "chrome-win64", "chrome.exe"), join(dir, "chrome-win", "chrome.exe")].find(
              existsSync,
            )
          : join(dir, "chrome-linux", "chrome");
    if (candidate && existsSync(candidate)) return candidate;
  }
  throw new Error(
    `No bundled Chromium under ${root}. Install one with the API build's driver (docs/09-operations-runbook.md §0) or, in src/frontend, run \`bunx playwright install chromium\`.`,
  );
}

const chromiumBin = bundledChromium();
const profileDir = mkdtempSync(join(tmpdir(), "bundled-shots-"));
const browser = Bun.spawn(
  [
    chromiumBin,
    "--headless",
    `--remote-debugging-port=${CDP}`,
    "--ignore-certificate-errors",
    `--user-data-dir=${profileDir}`,
    "about:blank",
  ],
  { stdout: "ignore", stderr: "ignore" },
);
let cdpUp = false;
for (let i = 0; i < 40 && !cdpUp; i++) {
  try {
    const r = await fetch(`http://127.0.0.1:${CDP}/json/version`);
    cdpUp = r.ok;
  } catch {
    // Not up yet.
  }
  if (!cdpUp) await sleep(500);
}
if (!cdpUp) {
  browser.kill();
  rmSync(profileDir, { recursive: true, force: true });
  throw new Error(
    `Bundled Chromium did not open a CDP endpoint on port ${CDP} in 20 s (${chromiumBin})`,
  );
}

const created = await fetch(`http://127.0.0.1:${CDP}/json/new?about:blank`, {
  method: "PUT",
}).then((r) => r.json());
const ws = new WebSocket(created.webSocketDebuggerUrl, {
  maxPayload: 256 * 1024 * 1024,
});
await new Promise<void>((resolve) =>
  ws.addEventListener("open", () => resolve(), { once: true }),
);

let id = 0;
const pending = new Map<number, (v: any) => void>();
ws.addEventListener("message", (e) => {
  const msg = JSON.parse(String(e.data));
  if (msg.id !== undefined && pending.has(msg.id)) {
    pending.get(msg.id)!(msg);
    pending.delete(msg.id);
  }
});
function send(method: string, params: any = {}): Promise<any> {
  const cur = ++id;
  ws.send(JSON.stringify({ id: cur, method, params }));
  return new Promise((resolve) => pending.set(cur, resolve));
}
const evalJs = async (expression: string) => {
  const r = await send("Runtime.evaluate", { expression, returnByValue: true });
  if (r.result?.exceptionDetails)
    throw new Error("evaluate failed: " + expression);
  return r.result.result.value;
};
async function ensureTheme(want: "LIGHT" | "DARK") {
  for (let i = 0; i < 40; i++) {
    const t = String(
      await evalJs(
        `document.querySelector('[title="Toggle color theme"]')?.textContent ?? "MISSING"`,
      ),
    );
    if (t.includes(want)) return;
    await evalJs(
      `document.querySelector('[title="Toggle color theme"]')?.click()`,
    );
    await sleep(400);
  }
  throw new Error("theme never reached " + want);
}
async function shot(path: string) {
  await send("Emulation.setDeviceMetricsOverride", {
    width: 1920,
    height: 6000,
    deviceScaleFactor: 1,
    mobile: false,
  });
  await sleep(800);
  const bottom = await evalJs(
    `(() => { const b = (s) => { const e = document.querySelector(s); if (!e) return 0; return e.getBoundingClientRect().bottom; }; return Math.ceil(Math.max(b('header'), b('main'), b('footer'), b('nav'))) + 48; })()`,
  );
  const h = Math.min(Math.max(bottom ?? 1080, 800), 6000);
  await send("Emulation.setDeviceMetricsOverride", {
    width: 1920,
    height: h,
    deviceScaleFactor: 1,
    mobile: false,
  });
  await sleep(600);
  const res = await send("Page.captureScreenshot", { format: "png" });
  await send("Emulation.clearDeviceMetricsOverride", {});
  await Bun.write(path, Buffer.from(res.result.data, "base64"));
  console.log("saved", path, `h=${h}`);
}

try {
  await send("Page.enable");
  await send("Runtime.enable");

  // Sign in once: every deck route sits behind the login gate. Fills the
  // React-controlled form via the native value setter so onInput fires,
  // submits, then polls for the deck theme toggle (absent on the login page).
  await send("Page.navigate", { url: `${BFF}/#/overview` });
  await sleep(2500);
  await evalJs(
    `(() => { const email = ${JSON.stringify(ADMIN_EMAIL)}; const pass = ${JSON.stringify(ADMIN_PASSWORD)}; const set = (el, v) => { const d = Object.getOwnPropertyDescriptor(el.__proto__, 'value') ?? Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value'); d?.set?.call(el, v); el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); }; const e = document.querySelector('input[name="email"]'); const p = document.querySelector('input[name="password"]'); if (e) set(e, email); if (p) set(p, pass); return !!(e && p); })()`,
  );
  await evalJs(
    `document.querySelector('form[aria-label="Sign in"] button[type="submit"]')?.click()`,
  );
  for (let i = 0; i < 40; i++) {
    const deck = await evalJs(
      `document.querySelector('[title="Toggle color theme"]') ? "READY" : "WAITING"`,
    );
    if (String(deck) === "READY") break;
    if (i === 39) throw new Error("deck never rendered after sign-in");
    await sleep(500);
  }

  for (const [slug, route] of routes) {
    await send("Page.navigate", { url: `${BFF}/#/${route}` });
    await sleep(3500);
    await ensureTheme("LIGHT");
    await shot(`${OUT}/${slug}-light.png`);
    await ensureTheme("DARK");
    await shot(`${OUT}/${slug}-dark.png`);
  }
} finally {
  ws.close();
  let cleaned = false;
  for (let i = 0; i < 3 && !cleaned; i++) {
    try {
      await fetch(`http://127.0.0.1:${CDP}/json/close/${created.id}`, {
        method: "PUT",
      });
    } catch {
      // Tab already gone. Nothing to clean.
    }
    await sleep(500);
    try {
      const list = await fetch(`http://127.0.0.1:${CDP}/json/list`).then((r) =>
        r.json(),
      );
      cleaned = !list.some((t: any) => t.id === created.id);
    } catch {
      cleaned = true;
    }
  }
  if (!cleaned)
    console.log("warning: capture tab still open, close it by hand");
  browser.kill();
  await browser.exited.catch(() => {});
  rmSync(profileDir, { recursive: true, force: true });
}
console.log("done");
