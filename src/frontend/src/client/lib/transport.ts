import type { SourceList } from "./api.ts";

/**
 * Whether a source runs over its HTTP API and never launches a browser. Reads the backend's
 * `apiOnly` flag from the sources snapshot; a missing snapshot or an unlisted source reads false,
 * which is the browser behavior the deck had before the flag existed.
 */
export function sourceApiOnly(sources: SourceList | null, sourceKey: string): boolean {
  const items = sources?.items;
  if (!Array.isArray(items)) {
    return false;
  }
  return items.some((source) => source.key === sourceKey && source.apiOnly === true);
}
