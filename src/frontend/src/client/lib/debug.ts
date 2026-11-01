import { useEffect, useRef } from "react";

/**
 * Render-rerender radar: every component calls {@link useRenderLog} so the browser console shows
 * exactly who renders, how often, and what remounts. Opt-in with `?renders=1`: logging every render
 * by default cost ~35 console calls a second on an idle deck, which an open devtools console turns
 * into real CPU and memory. Render counts are always kept (one map increment) so tests and the
 * console (`__deckRenders()`) can read them without the noise.
 */
function debugEnabled(): boolean {
  try {
    return new URLSearchParams(window.location.search).get("renders") === "1";
  } catch {
    // Non-browser (tests, SSR): stay quiet.
    return false;
  }
}

const enabled = typeof window === "undefined" ? false : debugEnabled();
const counts = new Map<string, number>();

if (typeof window !== "undefined") {
  (window as { __deckRenders?: () => Record<string, number> }).__deckRenders = () =>
    Object.fromEntries(counts);
}

/** True when render/fetch logs go to the console. */
export const renderDebugEnabled = enabled;

/** How many times `name` has rendered since the page loaded. */
export function renderCount(name: string): number {
  return counts.get(name) ?? 0;
}

export function useRenderLog(name: string): void {
  const count = useRef(0);
  count.current += 1;
  counts.set(name, (counts.get(name) ?? 0) + 1);
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
