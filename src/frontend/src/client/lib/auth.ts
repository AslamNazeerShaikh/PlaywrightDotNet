/** Permission checks and session-death routing that drive the shell. */

import { useEffect, useRef } from "react";

import type { ApiProblem, MeResponse } from "./api.ts";

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

/**
 * Routes a token-death problem to the shell's sign-out handler. Pages whose data lives in `App`
 * receive failures as props, so the redirect runs on the render that carries the failure; each
 * distinct problem routes once, and a later retry can route again.
 */
export function useTokenFailureRedirect(
  problems: ReadonlyArray<ApiProblem | null>,
  onUnauthorized: (problem: ApiProblem) => void,
): void {
  const routed = useRef<ApiProblem | null>(null);
  useEffect(() => {
    const problem = problems.find((p): p is ApiProblem => p !== null && isTokenFailure(p)) ?? null;
    if (problem !== null && routed.current !== problem) {
      routed.current = problem;
      onUnauthorized(problem);
    }
  }, [problems, onUnauthorized]);
}
