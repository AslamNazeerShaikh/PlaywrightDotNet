import type { MeResponse } from "./api.ts";

/**
 * Bearer token store plus the permission checks that drive the shell. The token lives in
 * `sessionStorage` (same tab only, cleared on close) and travels solely through the api client's
 * `getToken` hook, so no page ever touches it directly. It is never logged.
 */

const TOKEN_KEY = "jobscrape.bearerToken";

/** Reads the stored bearer token, or null outside a browser / when signed out. */
export function loadToken(storage: Storage | null = browserStorage()): string | null {
  try {
    const token = storage?.getItem(TOKEN_KEY) ?? null;
    return token === "" ? null : token;
  } catch {
    return null;
  }
}

/** Persists the bearer token for this tab. */
export function saveToken(token: string, storage: Storage | null = browserStorage()): void {
  try {
    storage?.setItem(TOKEN_KEY, token);
  } catch {
    // Private mode or disabled storage: the tab still works until reload.
  }
}

/** Drops the bearer token (sign out, or a 401 proving it stale). */
export function clearToken(storage: Storage | null = browserStorage()): void {
  try {
    storage?.removeItem(TOKEN_KEY);
  } catch {
    // Nothing to clean when storage is unavailable.
  }
}

function browserStorage(): Storage | null {
  try {
    return typeof sessionStorage === "undefined" ? null : sessionStorage;
  } catch {
    return null;
  }
}

/** True when the caller carries at least one of the keys. */
export function hasAnyPermission(me: MeResponse | null, ...keys: string[]): boolean {
  if (me === null) {
    return false;
  }
  return keys.some((key) => me.permissions.includes(key));
}

/** Whether a 401 means the token died (redirect to login) as opposed to a bad login attempt. */
export function isTokenFailure(problem: { status: number; code: string }): boolean {
  return problem.status === 401 && problem.code === "UNAUTHENTICATED";
}
