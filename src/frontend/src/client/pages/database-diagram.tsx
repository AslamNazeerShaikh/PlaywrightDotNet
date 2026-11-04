import { useRenderLog } from "../lib/debug.ts";
import type { DbTableSchema } from "../lib/api.ts";

const COLUMNS = 3;
const NODE_WIDTH = 200;
const NODE_HEIGHT = 30;
const GRID_GAP_X = 240;
const GRID_GAP_Y = 56;
const GRID_ORIGIN_X = 20;
const GRID_ORIGIN_Y = 20;

/**
 * The single database diagram. The entity lane is drawn from live schemas: one node per table,
 * a solid edge per foreign key, a dashed stub per table carrying source_key. The flow lane shows
 * data entering through runs and leaving through postings windows and exports.
 */
export function ErFlowDiagram({
  tables,
  schemas,
  selected = null,
}: {
  tables: string[];
  schemas: Record<string, DbTableSchema>;
  selected?: string | null;
}) {
  useRenderLog("ErFlowDiagram");
  const pos: Record<string, [number, number]> = {};
  tables.forEach((t, i) => {
    pos[t] = [
      GRID_ORIGIN_X + (i % COLUMNS) * GRID_GAP_X,
      GRID_ORIGIN_Y + Math.floor(i / COLUMNS) * GRID_GAP_Y,
    ];
  });
  const gridBottom =
    GRID_ORIGIN_Y + Math.ceil(Math.max(tables.length, 1) / COLUMNS) * GRID_GAP_Y;
  const flowTitleY = gridBottom + 56;
  const height = flowTitleY + 120;

  const edges: Array<{ from: [number, number]; to: [number, number]; label: string }> = [];
  for (const t of tables) {
    const schema = schemas[t];
    for (const fk of schema?.foreignKeys ?? []) {
      const a = pos[t];
      const b = pos[fk.referencedTable];
      if (a === undefined || b === undefined) {
        continue;
      }
      edges.push({
        from: [a[0] + NODE_WIDTH / 2, a[1] + NODE_HEIGHT],
        to: [b[0] + NODE_WIDTH / 2, b[1]],
        label: fk.column,
      });
    }
  }
  const busY = gridBottom + 16;
  const sourceKeyTables = tables.filter((t) =>
    (schemas[t]?.columns ?? []).some((c) => c.name === "source_key"),
  );

  return (
    <svg
      className="diagram"
      viewBox={`0 0 760 ${height}`}
      role="img"
      aria-label="Entity relationships and data flow"
    >
      {tables.map((t) => {
        const [x, y] = pos[t] ?? [GRID_ORIGIN_X, GRID_ORIGIN_Y];
        const label = t.length > 24 ? `${t.slice(0, 23)}…` : t;
        return (
          <g key={t}>
            <title>{t}</title>
            <rect
              x={x}
              y={y}
              width={NODE_WIDTH}
              height={NODE_HEIGHT}
              rx="4"
              style={{
                fill: "var(--canvas)",
                stroke: t === selected ? "var(--sig)" : "var(--line-hi)",
              }}
            />
            <text
              x={x + NODE_WIDTH / 2}
              y={y + 19}
              textAnchor="middle"
              style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
              fontWeight={t === selected ? "600" : undefined}
            >
              {label}
            </text>
          </g>
        );
      })}
      {edges.map((e, i) => (
        <g key={`fk-${i}`}>
          <line
            x1={e.from[0]}
            y1={e.from[1]}
            x2={e.to[0]}
            y2={e.to[1]}
            style={{ stroke: "var(--sig)" }}
            strokeWidth="1.5"
          />
          <text
            x={(e.from[0] + e.to[0]) / 2 + 4}
            y={(e.from[1] + e.to[1]) / 2}
            style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
          >
            {e.label}
          </text>
        </g>
      ))}
      {sourceKeyTables.length > 0 && (
        <g>
          <line
            x1={GRID_ORIGIN_X}
            y1={busY}
            x2={GRID_ORIGIN_X + COLUMNS * GRID_GAP_X - 40}
            y2={busY}
            style={{ stroke: "var(--dim)" }}
            strokeWidth="1"
            strokeDasharray="4 4"
          />
          {sourceKeyTables.map((t) => {
            const [x, y] = pos[t] ?? [GRID_ORIGIN_X, GRID_ORIGIN_Y];
            return (
              <line
                key={`bus-${t}`}
                x1={x + NODE_WIDTH / 2}
                y1={y + NODE_HEIGHT}
                x2={x + NODE_WIDTH / 2}
                y2={busY}
                style={{ stroke: "var(--dim)" }}
                strokeWidth="1"
                strokeDasharray="4 4"
              />
            );
          })}
          <text
            x={GRID_ORIGIN_X}
            y={busY + 14}
            style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
          >
            source_key shared value, no FK
          </text>
        </g>
      )}
      <g>
        <text
          x={GRID_ORIGIN_X}
          y={flowTitleY}
          style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
          fontWeight="600"
        >
          DATA IN AND OUT
        </text>
        <FlowLane y={flowTitleY + 18} />
      </g>
    </svg>
  );
}

/** Static map. Selection mirrors the table buttons below; the map itself never handles clicks. */
export function TablesDiagram({
  tables,
  selected,
}: {
  tables: Array<{ name: string; rowCount: number }>;
  selected: string | null;
}) {
  useRenderLog("TablesDiagram");
  const height =
    GRID_ORIGIN_Y +
    Math.ceil(Math.max(tables.length, 1) / COLUMNS) * GRID_GAP_Y +
    NODE_HEIGHT;
  return (
    <svg className="diagram" viewBox={`0 0 760 ${height}`} role="img" aria-label="Tables map">
      {tables.map((t, i) => {
        const x = GRID_ORIGIN_X + (i % COLUMNS) * GRID_GAP_X;
        const y = GRID_ORIGIN_Y + Math.floor(i / COLUMNS) * GRID_GAP_Y;
        const label = t.name.length > 24 ? `${t.name.slice(0, 23)}…` : t.name;
        return (
          <g key={t.name}>
            <title>{t.name}</title>
            <rect
              x={x}
              y={y}
              width={NODE_WIDTH}
              height={NODE_HEIGHT + 16}
              rx="4"
              style={{
                fill: "var(--canvas)",
                stroke: t.name === selected ? "var(--sig)" : "var(--line-hi)",
              }}
            />
            <text
              x={x + NODE_WIDTH / 2}
              y={y + 17}
              textAnchor="middle"
              style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
              fontWeight={t.name === selected ? "600" : undefined}
            >
              {label}
            </text>
            <text
              x={x + NODE_WIDTH / 2}
              y={y + 32}
              textAnchor="middle"
              style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
            >
              {`${t.rowCount} ${t.rowCount === 1 ? "row" : "rows"}`}
            </text>
          </g>
        );
      })}
    </svg>
  );
}

/** The flow lane: one path in through runs, two paths out through windows and files. */
function FlowLane({ y }: { y: number }) {
  const stops: Array<[number, string]> = [
    [20, "SOURCE SITES"],
    [180, "BFF :3000"],
    [340, ".NET :5208"],
    [500, "SQLITE STORE"],
  ];
  return (
    <g>
      {stops.map(([x, label]) => (
        <g key={label}>
          <rect
            x={x}
            y={y}
            width="140"
            height="28"
            rx="3"
            style={{ fill: "var(--canvas)", stroke: "var(--line-hi)" }}
          />
          <text
            x={x + 70}
            y={y + 18}
            textAnchor="middle"
            style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
            fontWeight="600"
          >
            {label}
          </text>
        </g>
      ))}
      <line
        x1={160}
        y1={y + 14}
        x2={180}
        y2={y + 14}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <line
        x1={320}
        y1={y + 14}
        x2={340}
        y2={y + 14}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <line
        x1={480}
        y1={y + 14}
        x2={500}
        y2={y + 14}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <text x={166} y={y + 8} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
        scrape
      </text>
      <text x={326} y={y + 8} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
        proxy
      </text>
      <text x={486} y={y + 8} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
        upsert
      </text>
      <line
        x1={570}
        y1={y + 28}
        x2={570}
        y2={y + 56}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <line
        x1={430}
        y1={y + 56}
        x2={710}
        y2={y + 56}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <line
        x1={430}
        y1={y + 56}
        x2={430}
        y2={y + 70}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <line
        x1={710}
        y1={y + 56}
        x2={710}
        y2={y + 70}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
      />
      <text x={438} y={y + 88} style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}>
        run postings window
      </text>
      <text x={438} y={y + 100} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
        LastSeenAt in [start, finish]
      </text>
      <text x={634} y={y + 88} style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}>
        csv / xlsx
      </text>
      <text x={634} y={y + 100} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
        export endpoints
      </text>
    </g>
  );
}
