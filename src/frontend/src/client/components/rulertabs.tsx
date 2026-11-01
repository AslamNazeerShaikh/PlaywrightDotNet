import { useRenderLog } from "../lib/debug.ts";
import { type DeckRoute, routeHash, ROUTES } from "../lib/router.ts";

/**
 * Ruler-tabs: ArtCraft-style measuring-rail navigation fused with the deck's radar language. Tick
 * marks and section digits form the rail; the pages are its stops; a glowing radar blip with a
 * sonar ping marks position and glides on the shared spring when the route changes. It IS the tab
 * bar — same links, same names, new instrument.
 */
export function RulerTabs({
  route,
  onSelect,
}: {
  route: DeckRoute;
  onSelect?: (route: DeckRoute) => void;
}) {
  useRenderLog("RulerTabs");
  const index = ROUTES.findIndex((r) => r.id === route);
  const stops = ROUTES.length;
  const left = `${((index * 2 + 1) / (stops * 2)) * 100}%`;

  return (
    <nav className="ruler" aria-label="Deck pages">
      <span className="ruler-code">NAV // {String(stops).padStart(2, "0")} STOPS</span>
      {/* --stops drives both the section ticks and the stop columns. */}
      <div className="ruler-track" style={{ "--stops": stops } as React.CSSProperties}>
        <div className="ruler-ticks" aria-hidden="true" />
        <div className="ruler-stops">
          {ROUTES.map((r) => (
            <a
              key={r.id}
              className="ruler-stop"
              href={routeHash(r.id)}
              aria-current={route === r.id ? "page" : undefined}
              onClick={() => onSelect?.(r.id)}
            >
              <span className="ruler-stop-code">{r.code}</span>
              {r.label.toUpperCase()}
            </a>
          ))}
        </div>
        <span key={route} className="ruler-blip" style={{ left }} aria-hidden="true">
          <span className="ruler-ping" />
        </span>
      </div>
      <span className="ruler-pos" aria-hidden="true">
        <strong>
          {String(index + 1).padStart(2, "0")}/{String(stops).padStart(2, "0")}
        </strong>
      </span>
    </nav>
  );
}
