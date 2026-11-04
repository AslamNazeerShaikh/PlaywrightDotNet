/** "08:00:00" from an ISO timestamp: the deck's clocks read HH:MM:SS, never dates. */
export function formatClock(iso: string): string {
  return iso.slice(11, 19);
}
