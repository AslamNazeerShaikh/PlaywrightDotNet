import { useRenderLog } from "../lib/debug.ts";
/**
 * Heading ribbon: a CoD-style compass tape with cardinal points, a swaying violet needle, and a
 * fixed per-page heading readout. The tape is static; only the needle hunts (transform-only sway),
 * so the strip reads as an instrument, not a distraction. The needle is its own small SVG laid over
 * the tape: swaying a shape inside the tape SVG (with its glow filter) repainted the whole strip
 * every frame on every page, while a standalone element's transform runs on the compositor. Cyan
 * carries information, violet carries attention — emerald stays reserved for health.
 */
export function Compass({ heading, page }: { heading: number; page: string }) {
  useRenderLog("Compass");
  const width = 640;
  const height = 46;
  const pad = 14;
  const ticks = [];
  for (let deg = 0; deg < 360; deg += 15) {
    const x = pad + (deg / 360) * (width - pad * 2);
    const major = deg % 45 === 0;
    ticks.push({ x, deg, major });
  }
  const label = (deg: number): string => {
    if (deg === 0) return "N";
    if (deg === 90) return "E";
    if (deg === 180) return "S";
    if (deg === 270) return "W";
    return String(deg).padStart(3, "0");
  };

  return (
    <div className="compass" role="img" aria-label={`Heading ${heading} degrees, ${page}`}>
      <span className="compass-hdg">
        HDG <strong>{String(heading).padStart(3, "0")}°</strong>
      </span>
      <span className="compass-dial">
        <svg viewBox={`0 0 ${width} ${height}`} aria-hidden="true">
          <line
            x1={pad}
            y1={height - 6}
            x2={width - pad}
            y2={height - 6}
            style={{ stroke: "var(--line-hi)" }}
          />
          {ticks.map((t) => (
            <g key={t.deg}>
              <line
                x1={t.x}
                y1={height - 6}
                x2={t.x}
                y2={t.major ? height - 20 : height - 13}
                style={{ stroke: "var(--brand-2)" }}
                opacity={t.major ? 0.9 : 0.45}
              />
              {t.major && (
                <text
                  x={t.x}
                  y={14}
                  textAnchor="middle"
                  style={{ fill: "var(--brand-2)", fontSize: "var(--text-micro)" }}
                  opacity="0.85"
                >
                  {label(t.deg)}
                </text>
              )}
            </g>
          ))}
        </svg>
        {/* Same user units as the tape (12 wide, centered), so it scales with it. */}
        <svg className="compass-needle" viewBox={`0 0 12 ${height}`} aria-hidden="true">
          <polygon points="6,4 0,22 12,22" style={{ fill: "var(--brand-3)" }} />
          <line
            x1={6}
            y1={22}
            x2={6}
            y2={height - 6}
            style={{ stroke: "var(--brand-3)" }}
            strokeWidth="1.5"
            opacity="0.7"
          />
        </svg>
      </span>
      <span className="compass-page">{page}</span>
    </div>
  );
}
