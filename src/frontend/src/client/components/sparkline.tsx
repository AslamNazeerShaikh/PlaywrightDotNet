import type { Sample } from "../lib/telemetry.ts";

/**
 * Latency sparkline: last N samples as an inline SVG that scales with its module (`viewBox` + width
 * 100%). Dots color by outcome; the deck never uses canvas here so the chart stays crisp and
 * test-queryable. Empty state draws the frame with a hint instead of an empty box.
 */
export function Sparkline({ samples, label }: { samples: Sample[]; label: string }) {
  const width = 300;
  const height = 84;
  const pad = 8;
  const max = Math.max(1, ...samples.map((s) => s.ms));
  const step = samples.length > 1 ? (width - pad * 2) / (samples.length - 1) : 0;

  const points = samples.map((s, i) => {
    const x = pad + i * step;
    const y = height - pad - (s.ms / max) * (height - pad * 2);
    return { x, y, ok: s.ok };
  });
  const line = points.map((p) => `${p.x.toFixed(1)},${p.y.toFixed(1)}`).join(" ");

  return (
    <svg
      className="diagram"
      viewBox={`0 0 ${width} ${height}`}
      role="img"
      aria-label={
        samples.length === 0 ? `${label}: no samples yet` : `${label}: ${samples.length} samples`
      }
    >
      <line
        x1={pad}
        y1={height - pad}
        x2={width - pad}
        y2={height - pad}
        style={{ stroke: "var(--line)" }}
      />
      {samples.length === 0 ? (
        <text
          x={width / 2}
          y={height / 2}
          textAnchor="middle"
          style={{ fill: "var(--faint)" }}
          fontSize="11"
        >
          AWAITING FIRST SAMPLE
        </text>
      ) : (
        <>
          {points.length > 1 && (
            <polyline
              points={line}
              fill="none"
              className="spark-draw"
              style={{ stroke: "var(--sig)" }}
              strokeWidth="1.5"
            />
          )}
          {points.map((p, i) => (
            <circle
              key={i}
              cx={p.x}
              cy={p.y}
              r="2.4"
              className="spark-dot"
              style={{
                fill: p.ok ? "var(--sig)" : "var(--red)",
                animationDelay: `${Math.min(i, 20) * 45}ms`,
              }}
            />
          ))}
          <text
            x={width - pad}
            y={pad + 4}
            textAnchor="end"
            style={{ fill: "var(--faint)" }}
            fontSize="10"
          >
            {`MAX ${max < 10 ? max.toFixed(1) : Math.round(max)} MS`}
          </text>
        </>
      )}
    </svg>
  );
}
