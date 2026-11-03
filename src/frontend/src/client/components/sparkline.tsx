import { useId, useState } from "react";

import { useRenderLog } from "../lib/debug.ts";
import type { Sample } from "../lib/telemetry.ts";

/**
 * Latency sparkline: last N samples as an inline SVG that scales with its module (`viewBox` + width
 * 100%). Dots color by outcome; the area under the line carries a signal gradient with a slow sheen
 * sweep: a gradient band clipped to the area, moved by a CSS transform (`.spark-sheen`). SMIL's
 * gradientTransform animation it replaced restyled the chart every frame. The deck never uses
 * canvas here so the chart stays crisp and test-queryable. Empty state draws the frame with a hint
 * instead of an empty box.
 */
export function Sparkline({ samples, label }: { samples: Sample[]; label: string }) {
  useRenderLog("Sparkline");
  const width = 300;
  const height = 84;
  const pad = 8;
  const max = Math.max(1, ...samples.map((s) => s.ms));
  const step = samples.length > 1 ? (width - pad * 2) / (samples.length - 1) : 0;
  // React useId contains colons, which break url(#…) references: strip them.
  const uid = useId().replace(/:/g, "");
  // The CSS kill-switch would park the sheen band mid-chart, so it only
  // mounts when motion is welcome.
  const [motionOK] = useState(
    () => window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches !== true,
  );

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
          style={{ fill: "var(--faint)", fontSize: "var(--text-label)" }}
        >
          AWAITING FIRST SAMPLE
        </text>
      ) : (
        <>
          <defs>
            <linearGradient id={`${uid}-area`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" style={{ stopColor: "var(--sig)", stopOpacity: 0.32 }} />
              <stop offset="100%" style={{ stopColor: "var(--sig)", stopOpacity: 0 }} />
            </linearGradient>
            {motionOK && (
              <>
                <linearGradient id={`${uid}-sheen`} x1="0" y1="0" x2="1" y2="0">
                  <stop offset="30%" stopColor="#ffffff" stopOpacity="0" />
                  <stop offset="50%" stopColor="#ffffff" stopOpacity="0.22" />
                  <stop offset="70%" stopColor="#ffffff" stopOpacity="0" />
                </linearGradient>
                <clipPath id={`${uid}-clip`}>
                  <polygon
                    points={`${line} ${width - pad},${height - pad} ${pad},${height - pad}`}
                  />
                </clipPath>
              </>
            )}
          </defs>
          {points.length > 1 && (
            <>
              <polygon
                points={`${line} ${width - pad},${height - pad} ${pad},${height - pad}`}
                fill={`url(#${uid}-area)`}
              />
              {motionOK && (
                <g clipPath={`url(#${uid}-clip)`}>
                  <rect
                    className="spark-sheen"
                    x={pad}
                    y={0}
                    width={width - pad * 2}
                    height={height}
                    fill={`url(#${uid}-sheen)`}
                  />
                </g>
              )}
              <polyline
                points={line}
                fill="none"
                className="spark-draw"
                style={{ stroke: "var(--sig)" }}
                strokeWidth="1.5"
              />
            </>
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
            style={{ fill: "var(--faint)", fontSize: "var(--text-micro)" }}
          >
            {`MAX ${max < 10 ? max.toFixed(1) : Math.round(max)} MS`}
          </text>
        </>
      )}
    </svg>
  );
}
