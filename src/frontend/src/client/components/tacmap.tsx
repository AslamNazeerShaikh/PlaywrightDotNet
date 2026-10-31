import { useId, useState } from "react";

import { useRenderLog } from "../lib/debug.ts";
import type { Sample } from "../lib/telemetry.ts";

/**
 * TAC-MAP: god's-eye view of the request battlespace. BFF is HQ at center; browser/API/store hold
 * cardinal positions. Range rings, a rotating sweep, packet tracers on live wires, and blips for
 * the last probes (X marks the misses). SMIL drives the sweep and tracers, so the map stays alive
 * with zero JS timers — and all of it unmounts under reduced motion.
 */
export function TacMap({
  samples,
  apiState,
}: {
  samples: Sample[];
  apiState: "unknown" | "live" | "down";
}) {
  useRenderLog("TacMap");
  // React useId contains colons, which break url(#…) references: strip them.
  const uid = useId().replace(/:/g, "");
  // SMIL ignores the CSS reduced-motion kill-switch, so self-animated layers
  // only mount when motion is welcome.
  const [motionOK] = useState(
    () => !window.matchMedia?.("(prefers-reduced-motion: reduce)").matches,
  );

  const cx = 320;
  const cy = 180;
  const live = apiState === "live";
  const wire = live ? "var(--sig)" : apiState === "down" ? "var(--red)" : "var(--faint)";
  const dashed = !live;

  const nodes = [
    { x: 320, y: 46, label: "BROWSER", sub: "deck", state: "var(--sig)" },
    { x: 320, y: 180, label: "BFF :3000", sub: "HQ", state: "var(--sig)" },
    { x: 534, y: 180, label: ".NET :5207", sub: "data plane", state: wire },
    { x: 320, y: 314, label: "STORE", sub: "in-memory", state: wire },
  ];
  const links: Array<[number, number]> = [
    [0, 1],
    [1, 2],
    [1, 3],
  ];

  const blips = samples.slice(-10).map((s, i) => {
    const angle = (((i * 47) % 360) * Math.PI) / 180;
    const radius = [70, 115, 150][i % 3] ?? 70;
    return { ...s, x: cx + radius * Math.cos(angle), y: cy + radius * Math.sin(angle) };
  });

  return (
    <svg
      className="diagram"
      viewBox="0 0 640 360"
      role="img"
      aria-label={
        samples.length === 0
          ? "Tactical overview map: no contacts yet"
          : `Tactical overview map: ${samples.length} contacts`
      }
    >
      <defs>
        <radialGradient id={`${uid}-sweep`}>
          <stop offset="0%" style={{ stopColor: "var(--sig)", stopOpacity: 0.28 }} />
          <stop offset="100%" style={{ stopColor: "var(--sig)", stopOpacity: 0 }} />
        </radialGradient>
      </defs>

      {gridLines()}
      {[55, 105, 155].map((r) => (
        <circle
          key={r}
          cx={cx}
          cy={cy}
          r={r}
          fill="none"
          style={{ stroke: "var(--line-hi)" }}
          strokeWidth="1"
          strokeDasharray="6 6"
          opacity="0.7"
        />
      ))}
      {cardinals(cx, cy)}

      {motionOK && (
        <g>
          <path
            d={`M ${cx} ${cy} L ${cx} 20 A 160 160 0 0 1 ${cx + 103} ${cy - 123} Z`}
            fill={`url(#${uid}-sweep)`}
          >
            <animateTransform
              attributeName="transform"
              type="rotate"
              from={`0 ${cx} ${cy}`}
              to={`360 ${cx} ${cy}`}
              dur="7s"
              repeatCount="indefinite"
            />
          </path>
          {/* HQ sonar: cyan range rings breathing outward. Additive — the emerald
              sweep keeps doing its job underneath. */}
          <circle
            cx={cx}
            cy={cy}
            r="17"
            fill="none"
            style={{ stroke: "var(--brand-2)" }}
            strokeWidth="1.5"
          >
            <animate attributeName="r" values="17;70" dur="3s" repeatCount="indefinite" />
            <animate attributeName="opacity" values="0.5;0" dur="3s" repeatCount="indefinite" />
          </circle>
          <circle
            cx={cx}
            cy={cy}
            r="17"
            fill="none"
            style={{ stroke: "var(--brand-3)" }}
            strokeWidth="1"
          >
            <animate
              attributeName="r"
              values="17;95"
              dur="4.5s"
              begin="1.5s"
              repeatCount="indefinite"
            />
            <animate
              attributeName="opacity"
              values="0.4;0"
              dur="4.5s"
              begin="1.5s"
              repeatCount="indefinite"
            />
          </circle>
        </g>
      )}

      {links.map(([a, b], i) => (
        <line
          key={i}
          x1={nodes[a]?.x}
          y1={nodes[a]?.y}
          x2={nodes[b]?.x}
          y2={nodes[b]?.y}
          style={{ stroke: wire }}
          strokeWidth="1.5"
          strokeDasharray={dashed ? "5 4" : undefined}
          className={dashed ? "flow-dash" : undefined}
        />
      ))}

      {motionOK && live && (
        <g>
          <path id={`${uid}-r1`} d="M 320 46 L 320 180 L 534 180" fill="none" />
          <path id={`${uid}-r2`} d="M 320 180 L 320 314" fill="none" />
          <circle r="3.5" style={{ fill: "var(--sig)" }}>
            <animateMotion dur="2.4s" repeatCount="indefinite">
              <mpath href={`#${uid}-r1`} />
            </animateMotion>
          </circle>
          <circle r="2.5" style={{ fill: "var(--sig)" }} opacity="0.8">
            <animateMotion dur="3.4s" repeatCount="indefinite">
              <mpath href={`#${uid}-r2`} />
            </animateMotion>
          </circle>
        </g>
      )}

      {blips.map((b, i) =>
        b.ok ? (
          <circle key={i} cx={b.x} cy={b.y} r="2.5" style={{ fill: "var(--sig)" }} opacity="0.9" />
        ) : (
          <g key={i} style={{ stroke: "var(--red)" }} strokeWidth="2">
            <line x1={b.x - 4} y1={b.y - 4} x2={b.x + 4} y2={b.y + 4} />
            <line x1={b.x + 4} y1={b.y - 4} x2={b.x - 4} y2={b.y + 4} />
            <circle cx={b.x} cy={b.y} r="7" fill="none" opacity="0.5" />
          </g>
        ),
      )}

      {nodes.map((n) => (
        <g key={n.label}>
          <rect
            x={n.x - 7}
            y={n.y - 7}
            width="14"
            height="14"
            rx="2"
            transform={`rotate(45 ${n.x} ${n.y})`}
            style={{ fill: "var(--canvas)", stroke: n.state }}
            strokeWidth="2"
          />
          {n.sub === "HQ" && (
            <circle
              cx={n.x}
              cy={n.y}
              r="17"
              fill="none"
              style={{ stroke: n.state }}
              strokeWidth="1"
              strokeDasharray="3 3"
            />
          )}
          <text
            x={n.x}
            y={n.y - 16}
            textAnchor="middle"
            style={{ fill: "var(--ink)", fontSize: "var(--text-label)" }}
            fontWeight="600"
          >
            {n.label}
          </text>
          <text
            x={n.x}
            y={n.y + 26}
            textAnchor="middle"
            style={{ fill: "var(--faint)", fontSize: "var(--text-micro)" }}
          >
            {n.sub}
          </text>
        </g>
      ))}
    </svg>
  );
}

function gridLines() {
  const lines = [];
  for (let x = 40; x < 640; x += 40) {
    lines.push(
      <line
        key={`v${x}`}
        x1={x}
        y1={0}
        x2={x}
        y2={360}
        style={{ stroke: "var(--line)" }}
        opacity="0.5"
      />,
    );
  }
  for (let y = 40; y < 360; y += 40) {
    lines.push(
      <line
        key={`h${y}`}
        x1={0}
        y1={y}
        x2={640}
        y2={y}
        style={{ stroke: "var(--line)" }}
        opacity="0.5"
      />,
    );
  }
  return <g>{lines}</g>;
}

function cardinals(cx: number, cy: number) {
  const marks = [
    { x: cx, y: 12, t: "N" },
    { x: 628, y: cy + 4, t: "E" },
    { x: cx, y: 352, t: "S" },
    { x: 12, y: cy + 4, t: "W" },
  ];
  return (
    <g>
      {marks.map((m) => (
        <text
          key={m.t}
          x={m.x}
          y={m.y}
          textAnchor="middle"
          style={{ fill: "var(--faint)", fontSize: "var(--text-label)" }}
          fontWeight="600"
        >
          {m.t}
        </text>
      ))}
    </g>
  );
}
