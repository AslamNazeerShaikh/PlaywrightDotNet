/**
 * Hash router for the ops deck. One page per feature, no dependency: the hash (`#/health`) is the
 * route, so deep links and refresh survive the BFF static server without any fallback
 * configuration. Parsing is pure and unit-tested.
 */

export type DeckRoute =
  | "overview"
  | "health"
  | "profiles"
  | "sources"
  | "sessions"
  | "runs"
  | "topology"
  | "database"
  | "users"
  | "roles"
  | "quotas";

export const ROUTES: ReadonlyArray<{ id: DeckRoute; code: string; label: string }> = [
  { id: "overview", code: "01", label: "Overview" },
  { id: "health", code: "02", label: "Engine Health" },
  { id: "profiles", code: "03", label: "Search Profiles" },
  { id: "sources", code: "04", label: "Sources" },
  { id: "sessions", code: "05", label: "Sessions" },
  { id: "runs", code: "06", label: "Runs" },
  { id: "topology", code: "07", label: "Topology" },
  { id: "database", code: "08", label: "Database" },
  { id: "users", code: "09", label: "Users" },
  { id: "roles", code: "10", label: "Roles" },
  { id: "quotas", code: "11", label: "Quotas" },
];

/** Parses a location hash into a route; unknown or empty hashes fall home. */
export function parseRoute(hash: string): DeckRoute {
  const match = /^#\/([a-z-]+)\/?$/.exec(hash.trim());
  const id = match?.[1];
  return ROUTES.find((r) => r.id === id)?.id ?? "overview";
}

/** Serializes a route back to its hash form. */
export function routeHash(route: DeckRoute): string {
  return `#/${route}`;
}
