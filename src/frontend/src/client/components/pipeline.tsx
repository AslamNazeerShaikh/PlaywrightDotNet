/**
 * Live request-path diagram: browser → BFF → .NET API → store. Node state is driven by the latest
 * probe outcome (unknown / live / down), so the picture is evidence, not decoration. Fluid SVG:
 * scales to module width, text never clips because every label sits inside its own node box.
 */
export function PipelineDiagram({
  bffState,
  apiState,
}: {
  bffState: "unknown" | "live" | "down";
  apiState: "unknown" | "live" | "down";
}) {
  const nodes = [
    { x: 8, label: "BROWSER", sub: "ops deck", state: "live" as const },
    { x: 168, label: "BFF :3000", sub: "/api/* proxy", state: bffState },
    { x: 328, label: ".NET :5207", sub: "data plane", state: apiState },
    { x: 488, label: "STORE", sub: "in-memory", state: apiState },
  ];
  const width = 640;
  const height = 120;

  return (
    <svg
      className="diagram"
      viewBox={`0 0 ${width} ${height}`}
      role="img"
      aria-label="Request path diagram"
    >
      {nodes.map((n, i) => (
        <g key={n.label}>
          {i > 0 && (
            <line
              x1={nodes[i - 1]?.x !== undefined ? (nodes[i - 1]?.x ?? 0) + 144 : 0}
              y1={height / 2}
              x2={n.x}
              y2={height / 2}
              style={{ stroke: linkColor(bffState, apiState, i) }}
              strokeWidth="1.5"
              strokeDasharray={
                linkColor(bffState, apiState, i) === "var(--faint)" ? "5 4" : undefined
              }
            />
          )}
          <rect
            x={n.x}
            y={height / 2 - 30}
            width="144"
            height="60"
            rx="4"
            style={{ fill: "var(--canvas)", stroke: nodeColor(n.state) }}
            strokeWidth="1.5"
          />
          <circle cx={n.x + 14} cy={height / 2 - 12} r="4" style={{ fill: nodeColor(n.state) }} />
          <text
            x={n.x + 26}
            y={height / 2 - 8}
            style={{ fill: "var(--ink)" }}
            fontSize="12"
            fontWeight="600"
          >
            {n.label}
          </text>
          <text x={n.x + 14} y={height / 2 + 12} style={{ fill: "var(--faint)" }} fontSize="10">
            {n.sub}
          </text>
        </g>
      ))}
    </svg>
  );
}

function nodeColor(state: "unknown" | "live" | "down"): string {
  if (state === "live") return "var(--sig)";
  if (state === "down") return "var(--red)";
  return "var(--faint)";
}

/** Link i connects node i-1 → i: browser→BFF rides the BFF state, the rest the API state. */
function linkColor(
  bff: "unknown" | "live" | "down",
  api: "unknown" | "live" | "down",
  i: number,
): string {
  const state = i === 1 ? bff : api;
  if (state === "live") return "var(--sig)";
  if (state === "down") return "var(--red)";
  return "var(--faint)";
}
