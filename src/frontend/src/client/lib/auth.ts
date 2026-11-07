/** Permission checks that drive the shell. */

import type { MeResponse } from "./api.ts";

/** True when the caller carries at least one of the keys. */
export function hasAnyPermission(me: MeResponse | null, ...keys: string[]): boolean {
  if (me === null) {
    return false;
  }
  return keys.some((key) => me.permissions.includes(key));
}

/** Whether a 401 means the token died (redirect to login) as opposed to a bad login attempt. */
export function isTokenFailure(problem: { status: number; code: string }): boolean {
  return (
    problem.status === 401 &&
    (problem.code === "UNAUTHENTICATED" || problem.code === "SESSION_REVOKED")
  );
}
