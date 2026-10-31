/**
 * Hash router for the ops deck. One page per feature, no dependency: the hash (`#/health`) is the
 * route, so deep links and refresh survive the BFF static server without any fallback
 * configuration. Parsing is pure and unit-tested.
 */

export type DeckRoute = "overview" | "health" | "profiles" | "topology";

export const ROUTES: ReadonlyArray<{ id: DeckRoute; code: string; label: string }> = [
  { id: "overview", code: "01", label: "Overview" },
  { id: "health", code: "02", label: "Engine Health" },
  { id: "profiles", code: "03", label: "Search Profiles" },
  { id: "topology", code: "04", label: "Topology" },
];

/** Parses a location hash into a route; unknown or empty hashes fall home. */
export function parseRoute(hash: string): DeckRoute {
  const match = /^#\/([a-z-]+)\/?$/.exec(hash.trim());
  const id = match?.[1];
  if (id === "overview" || id === "health" || id === "profiles" || id === "topology") {
    return id;
  }
  return "overview";
}

/** Serializes a route back to its hash form. */
export function routeHash(route: DeckRoute): string {
  return `#/${route}`;
}
