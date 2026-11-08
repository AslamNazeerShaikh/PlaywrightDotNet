import {
  accentTone,
  actionLabel,
  DiagramDefs,
  type DiagramTone,
  markerId,
  TONE,
} from "../components/diagram.tsx";
import type { DbColumnInfo, DbTableSchema } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";

// The one FK-enforced run subsystem: search_profiles <- scrape_runs,
// scrape_run_progress -> scrape_runs. The rest share values, not keys, so the panel
// argues this subsystem instead of a neighbourhood map.
const FOCUS = ["search_profiles", "scrape_runs", "scrape_run_progress"] as const;

// Lead columns per table; the rest fall back to schema order. Keeps the FK
// endpoints and the run lifecycle in view before any overflow cut.
const STORY: Record<string, readonly string[]> = {
  scrape_runs: ["profile_id", "id", "stage", "started_at", "finished_at", "retry_at", "created_at"],
};

const X_LEFT = 24;
const X_RIGHT = 540;
const TW = 330;
const HEAD = 34;
const ROW = 24;
const MAX_ROWS = 8;
const LANE_BASE = 430;
const LANE_GAP = 30;
const CORNER = 8;
const BOX_Y: Record<string, number> = {
  search_profiles: 28,
  scrape_runs: 160,
  scrape_run_progress: 310,
};
const SIDE: Record<string, "left" | "right"> = {
  search_profiles: "left",
  scrape_runs: "right",
  scrape_run_progress: "left",
};

interface ErdBox {
  name: string;
  x: number;
  y: number;
  height: number;
  rows: Array<{ column: DbColumnInfo; centre: number; stripe: boolean; fk: boolean }>;
  hidden: number;
  headerFocal: boolean;
}

interface ErdEdge {
  fromTable: string;
  fromColumn: string;
  toTable: string;
  toColumn: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  lane: number;
  onDelete: string | null;
  tone: DiagramTone;
}

function shownColumns(schema: DbTableSchema): { shown: DbColumnInfo[]; hidden: number } {
  const ordered: DbColumnInfo[] = [];
  const seen = new Set<string>();
  const push = (name: string) => {
    const column = schema.columns.find((c) => c.name === name);
    if (column !== undefined && !seen.has(column.name)) {
      seen.add(column.name);
      ordered.push(column);
    }
  };
  for (const name of STORY[schema.name] ?? []) {
    push(name);
  }
  for (const column of schema.columns) {
    if (column.primaryKey) {
      push(column.name);
    }
  }
  for (const fk of schema.foreignKeys) {
    push(fk.column);
  }
  for (const column of schema.columns) {
    push(column.name);
  }
  const shown = ordered.slice(0, MAX_ROWS);
  return { shown, hidden: ordered.length - shown.length };
}

function innerX(name: string): number {
  return SIDE[name] === "left" ? X_LEFT + TW : X_RIGHT;
}

function buildErd(schemas: Record<string, DbTableSchema>): { boxes: ErdBox[]; edges: ErdEdge[] } {
  const present = FOCUS.filter((name) => schemas[name] !== undefined);
  const boxes: ErdBox[] = present.map((name) => {
    const schema = schemas[name]!;
    const { shown, hidden } = shownColumns(schema);
    const fkColumns = new Set(schema.foreignKeys.map((fk) => fk.column));
    const y = BOX_Y[name]!;
    const side = SIDE[name]!;
    return {
      name,
      x: side === "left" ? X_LEFT : X_RIGHT,
      y,
      height: HEAD + shown.length * ROW + (hidden > 0 ? ROW : 0),
      rows: shown.map((column, index) => ({
        column,
        centre: y + HEAD + index * ROW + ROW / 2,
        stripe: index % 2 === 1,
        fk: fkColumns.has(column.name),
      })),
      hidden,
      headerFocal: false,
    };
  });
  const byName = new Map(boxes.map((box) => [box.name, box]));
  const edges: ErdEdge[] = [];
  for (const box of boxes) {
    const schema = schemas[box.name]!;
    for (const fk of schema.foreignKeys) {
      const target = byName.get(fk.referencedTable);
      if (target === undefined) {
        continue;
      }
      const sourceRow = box.rows.find((row) => row.column.name === fk.column);
      const targetRow = target.rows.find((row) => row.column.name === fk.referencedColumn);
      if (sourceRow === undefined || targetRow === undefined) {
        continue;
      }
      edges.push({
        fromTable: box.name,
        fromColumn: fk.column,
        toTable: fk.referencedTable,
        toColumn: fk.referencedColumn,
        x1: innerX(box.name),
        y1: sourceRow.centre,
        x2: innerX(target.name),
        y2: targetRow.centre,
        lane: 0,
        onDelete: fk.onDelete ?? null,
        tone: accentTone(fk.onDelete ?? null),
      });
    }
  }
  // Lane order follows the source box so the run chain stays adjacent and the
  // two vertical spans never overlap the labels.
  edges.sort((a, b) => a.y1 - b.y1 || a.fromTable.localeCompare(b.fromTable));
  edges.forEach((edge, index) => {
    edge.lane = LANE_BASE + index * LANE_GAP;
  });
  for (const box of boxes) {
    box.headerFocal = edges.some((edge) => edge.fromTable === box.name && edge.tone === "red");
  }
  return { boxes, edges };
}

function elbowPath(edge: ErdEdge): string {
  const dirH1 = Math.sign(edge.lane - edge.x1) || 1;
  const dirV = Math.sign(edge.y2 - edge.y1) || 1;
  const dirH2 = Math.sign(edge.x2 - edge.lane) || 1;
  const r = Math.min(
    CORNER,
    Math.abs(edge.y2 - edge.y1) / 2,
    Math.abs(edge.lane - edge.x1) / 2,
    Math.abs(edge.x2 - edge.lane) / 2,
  );
  return [
    `M ${edge.x1},${edge.y1}`,
    `L ${edge.lane - dirH1 * r},${edge.y1}`,
    `Q ${edge.lane},${edge.y1} ${edge.lane},${edge.y1 + dirV * r}`,
    `V ${edge.y2 - dirV * r}`,
    `Q ${edge.lane},${edge.y2} ${edge.lane + dirH2 * r},${edge.y2}`,
    `L ${edge.x2},${edge.y2}`,
  ].join(" ");
}

function maskWidth(label: string): number {
  return label.length * 5.5 + 6;
}

/**
 * Column-level ERD of the run subsystem, drawn from the live schema payload. The three boxes are
 * fixed; every column row, FK anchor, and action label comes from the `schemas` prop, never from a
 * checked-in snapshot.
 */
export function SchemaErd({
  schemas,
  selected,
  counts = {},
}: {
  schemas: Record<string, DbTableSchema>;
  selected: string | null;
  counts?: Record<string, number>;
}) {
  useRenderLog("SchemaErd");
  const { boxes, edges } = buildErd(schemas);
  const totalTables = Object.keys(schemas).length;

  if (boxes.length === 0) {
    return (
      <>
        <svg
          className="diagram"
          viewBox="0 0 360 72"
          role="img"
          aria-label="Column-level ERD of the run subsystem"
        >
          <text
            x={180}
            y={40}
            textAnchor="middle"
            style={{ fill: "var(--dim)", fontSize: "var(--text-micro)" }}
          >
            The run subsystem is not in the live schema yet.
          </text>
        </svg>
        <p className="status-line">No enforced foreign keys in the live schema.</p>
      </>
    );
  }

  const selectedOutside = selected !== null && !boxes.some((box) => box.name === selected);
  const missingAction = edges.some((edge) => edge.onDelete === null);
  const caption = [
    `Physical-FK subsystem: ${boxes.length} of ${totalTables} live tables.`,
    edges.length > 0
      ? `${edges
          .map(
            (edge) =>
              `${edge.fromTable}.${edge.fromColumn} → ${edge.toTable}.${edge.toColumn}${
                edge.onDelete === null ? "" : ` (ON DELETE ${edge.onDelete})`
              }`,
          )
          .join(" · ")}.`
      : null,
    missingAction ? "The API does not expose ON DELETE actions." : null,
    selectedOutside ? `Selected ${selected} is outside this subsystem.` : null,
  ]
    .filter((part) => part !== null)
    .join(" ");

  return (
    <>
      <svg
        className="diagram"
        viewBox="0 0 880 480"
        role="img"
        aria-label="Column-level ERD of the run subsystem"
      >
        <DiagramDefs prefix="schema-erd" />
        {edges.map((edge) => {
          const action = actionLabel(edge.onDelete);
          const label = action === null ? "" : `ON DELETE ${action}`;
          const midY = (Math.min(edge.y1, edge.y2) + Math.max(edge.y1, edge.y2)) / 2;
          const startAnchored = SIDE[edge.fromTable] === "right";
          const labelX = startAnchored ? edge.lane + 6 : edge.lane - 6;
          const width = maskWidth(label);
          return (
            <g key={`${edge.fromTable}.${edge.fromColumn}`}>
              <path
                data-fk
                data-from={`${edge.fromTable}.${edge.fromColumn}`}
                data-to={`${edge.toTable}.${edge.toColumn}`}
                {...(action === null ? {} : { "data-action": action })}
                d={elbowPath(edge)}
                fill="none"
                style={{ stroke: TONE[edge.tone].stroke }}
                strokeWidth="1.5"
                markerEnd={`url(#${markerId("schema-erd", edge.tone)})`}
              />
              {action !== null && (
                <>
                  <rect
                    x={startAnchored ? labelX - 2 : labelX - width + 2}
                    y={midY - 9}
                    width={width}
                    height={11}
                    style={{ fill: "var(--void)" }}
                  />
                  <text
                    x={labelX}
                    y={midY}
                    textAnchor={startAnchored ? "start" : "end"}
                    style={{ fill: TONE[edge.tone].text, fontSize: "var(--text-fine)" }}
                  >
                    {label}
                  </text>
                </>
              )}
            </g>
          );
        })}
        {boxes.map((box) => (
          <g key={box.name} data-table={box.name}>
            <title>{box.name}</title>
            <rect
              className="erd-box"
              x={box.x}
              y={box.y}
              width={TW}
              height={box.height}
              rx="6"
              style={{ fill: "var(--panel)" }}
            />
            <rect
              className="erd-head"
              x={box.x}
              y={box.y}
              width={TW}
              height={HEAD}
              rx="6"
              style={{ fill: box.headerFocal ? "var(--diagram-red-soft)" : "var(--raise)" }}
            />
            <line
              x1={box.x}
              y1={box.y + HEAD}
              x2={box.x + TW}
              y2={box.y + HEAD}
              style={{ stroke: "var(--line)" }}
              strokeWidth="1"
            />
            {box.rows.map(
              (row, index) =>
                row.stripe && (
                  <rect
                    key={`stripe-${row.column.name}`}
                    x={box.x}
                    y={box.y + HEAD + index * ROW}
                    width={TW}
                    height={ROW}
                    style={{ fill: "var(--diagram-gray-soft)" }}
                  />
                ),
            )}
            <rect
              x={box.x + 12}
              y={box.y + 7}
              width={34}
              height={11}
              rx="2"
              style={{ fill: "none", stroke: "var(--faint)" }}
              strokeWidth="1"
            />
            <text
              x={box.x + 29}
              y={box.y + 15.5}
              textAnchor="middle"
              style={{ fill: "var(--faint)", fontSize: "8px" }}
            >
              TABLE
            </text>
            <text
              x={box.x + 52}
              y={box.y + 22}
              style={{ fill: "var(--ink)", fontSize: "12px" }}
              fontWeight="600"
            >
              {box.name}
            </text>
            <text
              x={box.x + TW - 12}
              y={box.y + 22}
              textAnchor="end"
              style={{ fill: "var(--dim)", fontSize: "9px" }}
            >
              {counts[box.name] === undefined
                ? "— rows"
                : counts[box.name] === 1
                  ? "1 row"
                  : `${counts[box.name]} rows`}
            </text>
            {box.rows.map((row, index) => {
              const chips: Array<{ label: string; tone: DiagramTone }> = [];
              if (row.column.primaryKey) {
                chips.push({ label: "PK", tone: "green" });
              }
              if (row.fk) {
                chips.push({ label: "FK", tone: "cyan" });
                if (row.column.notNull) {
                  chips.push({ label: "NN", tone: "gray" });
                }
              }
              let chipX = box.x + 158;
              return (
                <g key={row.column.name} data-row={row.column.name}>
                  <text
                    x={box.x + 12}
                    y={box.y + HEAD + index * ROW + 16}
                    style={{ fill: "var(--ink)", fontSize: "12px" }}
                  >
                    {row.column.name}
                  </text>
                  {chips.map((chip) => {
                    const width = chip.label.length * 5.5 + 8;
                    const x = chipX;
                    chipX += width + 4;
                    return (
                      <g key={chip.label}>
                        <rect
                          x={x}
                          y={box.y + HEAD + index * ROW + 8}
                          width={width}
                          height={10}
                          rx="2"
                          style={{
                            fill: "none",
                            stroke: TONE[chip.tone].stroke,
                          }}
                        />
                        <text
                          x={x + width / 2}
                          y={box.y + HEAD + index * ROW + 16}
                          textAnchor="middle"
                          style={{ fill: TONE[chip.tone].text, fontSize: "8px" }}
                        >
                          {chip.label}
                        </text>
                      </g>
                    );
                  })}
                  <text
                    x={box.x + TW - 12}
                    y={box.y + HEAD + index * ROW + 16}
                    textAnchor="end"
                    style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
                  >
                    {row.column.type || "—"}
                  </text>
                </g>
              );
            })}
            {box.hidden > 0 && (
              <text
                data-overflow={box.hidden}
                x={box.x + 12}
                y={box.y + HEAD + box.rows.length * ROW + 16}
                style={{ fill: "var(--faint)", fontSize: "9px" }}
              >
                {`+ ${box.hidden} more columns`}
              </text>
            )}
            <rect
              className="erd-outline"
              x={box.x}
              y={box.y}
              width={TW}
              height={box.height}
              rx="6"
              fill="none"
              style={{ stroke: selected === box.name ? "var(--sig)" : "var(--line-hi)" }}
              strokeWidth={selected === box.name ? 2 : 1}
            />
          </g>
        ))}
      </svg>
      <p className="status-line">{caption}</p>
    </>
  );
}
