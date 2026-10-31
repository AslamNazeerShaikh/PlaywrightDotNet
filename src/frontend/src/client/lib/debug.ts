import { useEffect, useRef } from "react";

/**
 * Render-rerender radar: every component calls {@link useRenderLog} so the browser console shows
 * exactly who renders, how often, and what remounts. Loud in development, silent in production
 * builds and under `bun test`. Force it anywhere with `?renders=1` (or kill it with `?renders=0`).
 */
function debugEnabled(): boolean {
  try {
    const forced = new URLSearchParams(window.location.search).get("renders");
    if (forced === "1") {
      return true;
    }
    if (forced === "0") {
      return false;
    }
  } catch {
    // Non-browser (tests, SSR): fall through to the env gate below.
  }
  const env = typeof process === "undefined" ? undefined : process.env["NODE_ENV"];
  return env !== "production" && env !== "test";
}

const enabled = typeof window === "undefined" ? false : debugEnabled();

/** True when render/fetch logs go to the console. */
export const renderDebugEnabled = enabled;

export function useRenderLog(name: string): void {
  const count = useRef(0);
  count.current += 1;
  if (enabled) {
    // Intentional side effect: this hook exists to observe renders.
    console.debug(`[render] ${name} #${count.current}`);
  }
  useEffect(() => {
    if (!enabled) {
      return;
    }
    console.debug(`[mount] ${name}`);
    return () => {
      console.debug(`[unmount] ${name}`);
    };
  }, [name]);
}
