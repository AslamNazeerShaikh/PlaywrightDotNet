import {
  DiagramDefs,
  type DiagramTone,
  markerId,
  startMarkerId,
  TONE,
} from "../components/diagram.tsx";
import type { DbTableSchema } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";

type TableGroup = "source" | "runs" | "postings" | "browser" | "system";

const GROUP_ORDER: TableGroup[] = ["source", "runs", "postings", "browser", "system"];

const GROUP_LABEL: Record<TableGroup, string> = {
  source: "SOURCE & PROFILE",
  runs: "RUN STATE",
  postings: "POSTINGS",
  browser: "BROWSER",
  system: "EF SYSTEM",
};

const GROUP_TONE: Record<TableGroup, DiagramTone> = {
  source: "cyan",
  runs: "green",
  postings: "amber",
  browser: "violet",
  system: "gray",
};

/** Classifies a live table into its domain lane. Unknown tables fall into the system-neutral lane. */
function groupOf(name: string): TableGroup {
  if (name.startsWith("__EF")) {
    return "system";
  }
  if (name.startsWith("scrape_run")) {
    return "runs";
  }
  if (name === "job_postings") {
    return "postings";
  }
  if (name === "browser_profiles") {
    return "browser";
  }
  return "source";
}

function truncateName(name: string, max: number): string {
  return name.length > max ? `${name.slice(0, max - 1)}…` : name;
}

function rowsLabel(count: number): string {
  return `${count} ${count === 1 ? "row" : "rows"}`;
}

const CARD_H = 48;
// Empty map keeps a readable 5:1 frame instead of collapsing to a sliver of viewBox.
const EMPTY_VIEW_W = 360;
const EMPTY_VIEW_H = 72;
const CARD_GAP = 8;
const GROUP_COL = 208;
const GROUP_GAP_X = 12;
const GROUP_ORIGIN_X = 10;
const GROUP_ORIGIN_Y = 8;
const GROUP_HEAD = 24;
const GROUP_PAD = 8;

/**
 * DB-00 table map: one column per domain, colored by the lane it owns, with every live table as a
 * card. The list beside it remains the click target; this map is the at-a-glance inventory.
 */
export function TablesDiagram({
  tables,
  selected,
  schemas = {},
}: {
  tables: Array<{ name: string; rowCount: number }>;
  selected: string | null;
  schemas?: Record<string, DbTableSchema>;
}) {
  useRenderLog("TablesDiagram");
  const groups = GROUP_ORDER.map((group) => ({
    group,
    items: tables.filter((table) => groupOf(table.name) === group),
  })).filter((entry) => entry.items.length > 0);

  const layouts = groups.map(({ group, items }, index) => {
    const x = GROUP_ORIGIN_X + index * (GROUP_COL + GROUP_GAP_X);
    const height = GROUP_HEAD + items.length * CARD_H + (items.length - 1) * CARD_GAP + GROUP_PAD;
    const cards = items.map((table, cardIndex) => ({
      ...table,
      x: x + GROUP_PAD,
      y: GROUP_ORIGIN_Y + GROUP_HEAD + cardIndex * (CARD_H + CARD_GAP),
    }));
    return { group, x, height, items, cards };
  });
  const isEmpty = layouts.length === 0;
  const height = isEmpty
    ? EMPTY_VIEW_H
    : Math.max(...layouts.map((layout) => layout.height), 40) + GROUP_ORIGIN_Y * 2;
  const viewWidth = isEmpty
    ? EMPTY_VIEW_W
    : GROUP_ORIGIN_X * 2 +
      layouts.length * GROUP_COL +
      Math.max(layouts.length - 1, 0) * GROUP_GAP_X;

  return (
    <svg
      className="diagram"
      viewBox={`0 0 ${viewWidth} ${height}`}
      role="img"
      aria-label="Tables map"
    >
      {isEmpty && (
        <text
          x={EMPTY_VIEW_W / 2}
          y={40}
          textAnchor="middle"
          style={{ fill: "var(--dim)", fontSize: "var(--text-micro)" }}
        >
          No user tables yet.
        </text>
      )}
      {layouts.map(({ group, x, height: groupHeight, items, cards }) => {
        const tone = GROUP_TONE[group];
        return (
          <g key={group}>
            <rect
              x={x}
              y={GROUP_ORIGIN_Y}
              width={GROUP_COL}
              height={groupHeight}
              rx="8"
              strokeDasharray="5 4"
              style={{ fill: TONE[tone].fill, stroke: TONE[tone].stroke }}
            />
            <circle
              cx={x + 11}
              cy={GROUP_ORIGIN_Y + 13}
              r="3"
              style={{ fill: TONE[tone].stroke }}
            />
            <text
              x={x + 19}
              y={GROUP_ORIGIN_Y + 16}
              style={{ fill: TONE[tone].text, fontSize: "var(--text-fine)" }}
              fontWeight="600"
              letterSpacing="0.06em"
            >
              {GROUP_LABEL[group]}
            </text>
            <text
              x={x + GROUP_COL - 8}
              y={GROUP_ORIGIN_Y + 16}
              textAnchor="end"
              style={{ fill: "var(--faint)", fontSize: "var(--text-fine)" }}
            >
              {`${items.length} ${items.length === 1 ? "TABLE" : "TABLES"}`}
            </text>
            {cards.map((table) => (
              <TableCard
                key={table.name}
                name={table.name}
                rowCount={table.rowCount}
                schema={schemas[table.name]}
                x={table.x}
                y={table.y}
                w={GROUP_COL - GROUP_PAD * 2}
                h={CARD_H}
                tone={tone}
                selected={table.name === selected}
                compact
              />
            ))}
          </g>
        );
      })}
    </svg>
  );
}

const ER_VIEW_W = 1360;
const BUS_Y = 30;
const GROUP_TOP = 42;

const ER_GROUPS: Array<{ group: TableGroup; x: number; y: number; w: number; h: number }> = [
  { group: "source", x: 24, y: GROUP_TOP, w: 306, h: 286 },
  { group: "runs", x: 404, y: GROUP_TOP, w: 286, h: 216 },
  { group: "postings", x: 764, y: GROUP_TOP, w: 286, h: 216 },
  { group: "browser", x: 1124, y: GROUP_TOP, w: 200, h: 110 },
  { group: "system", x: 1124, y: 180, w: 200, h: 110 },
];

const ER_CARDS: Array<{
  name: string;
  group: TableGroup;
  x: number;
  y: number;
  w: number;
  h: number;
}> = [
  { name: "search_profiles", group: "source", x: 38, y: 78, w: 278, h: 58 },
  { name: "source_quotas", group: "source", x: 38, y: 160, w: 278, h: 58 },
  { name: "source_circuit_breakers", group: "source", x: 38, y: 242, w: 278, h: 58 },
  { name: "scrape_runs", group: "runs", x: 418, y: 78, w: 258, h: 58 },
  { name: "scrape_run_progress", group: "runs", x: 418, y: 166, w: 258, h: 58 },
  { name: "job_postings", group: "postings", x: 778, y: 78, w: 258, h: 58 },
  { name: "browser_profiles", group: "browser", x: 1136, y: 78, w: 176, h: 58 },
  { name: "__EFMigrationsHistory", group: "system", x: 1136, y: 206, w: 176, h: 34 },
  { name: "__EFMigrationsLock", group: "system", x: 1136, y: 250, w: 176, h: 34 },
];

const KNOWN_TABLES = new Set(ER_CARDS.map((card) => card.name));

/**
 * DB-03 shape and flow. The top lane is drawn from the live schema: domain groups, the one enforced
 * FK, the shared source_key identity, the run-progress identity, and the time-window link from a
 * run to its postings. The bottom lanes correct the runtime order: Playwright scrapes inside .NET,
 * SQLite is the only store, and the BFF only proxies the browser's API calls.
 */
export function ErFlowDiagram({
  tables,
  schemas,
  selected = null,
  counts = {},
}: {
  tables: string[];
  schemas: Record<string, DbTableSchema>;
  selected?: string | null;
  counts?: Record<string, number>;
}) {
  useRenderLog("ErFlowDiagram");
  const present = new Set(tables);
  const other = tables.filter((name) => !KNOWN_TABLES.has(name));
  const otherRows = Math.ceil(other.length / 5);
  const otherY = 350;
  const otherH = otherRows > 0 ? 38 + otherRows * 36 + (otherRows - 1) * 10 : 0;
  const entityBottom = otherRows > 0 ? otherY + otherH : 328;
  const flowTop = entityBottom + 30;

  const presentGroups = new Set(
    ER_CARDS.filter((card) => present.has(card.name)).map((card) => card.group),
  );
  const busGroups = ER_GROUPS.filter(
    (entry) => entry.group !== "system" && presentGroups.has(entry.group),
  );
  const busStart = busGroups.length > 0 ? busGroups[0]!.x + busGroups[0]!.w / 2 : 0;
  const busEnd =
    busGroups.length > 0
      ? busGroups[busGroups.length - 1]!.x + busGroups[busGroups.length - 1]!.w / 2
      : 0;

  const writeY = flowTop + 50;
  const readY = flowTop + 200;
  const boxW = 280;
  const boxH = 60;
  const laneX = [75, 385, 695, 1005];

  return (
    <svg
      className="diagram"
      viewBox={`0 0 ${ER_VIEW_W} ${flowTop + 312}`}
      role="img"
      aria-label="Entity relationships and data flow"
    >
      <DiagramDefs prefix="db-03" />

      {busGroups.length > 1 && (
        <g>
          <line
            x1={busStart}
            y1={BUS_Y}
            x2={busEnd}
            y2={BUS_Y}
            strokeDasharray="5 5"
            style={{ stroke: "var(--brand-3)" }}
            strokeWidth="1.2"
          />
          {busGroups.map((entry) => (
            <line
              key={`bus-${entry.group}`}
              x1={entry.x + entry.w / 2}
              y1={BUS_Y}
              x2={entry.x + entry.w / 2}
              y2={GROUP_TOP}
              strokeDasharray="5 5"
              style={{ stroke: "var(--brand-3)" }}
              strokeWidth="1.2"
            />
          ))}
          <text
            x={Math.max(24, busStart - 190)}
            y={18}
            style={{ fill: "var(--brand-3)", fontSize: "var(--text-fine)" }}
          >
            source_key · shared value, no FK
          </text>
        </g>
      )}

      {ER_GROUPS.filter((entry) => presentGroups.has(entry.group)).map((entry) => (
        <GroupBox key={entry.group} {...entry} />
      ))}

      {ER_CARDS.filter((card) => present.has(card.name)).map((card) => (
        <TableCard
          key={card.name}
          name={card.name}
          rowCount={counts[card.name]}
          schema={schemas[card.name]}
          x={card.x}
          y={card.y}
          w={card.w}
          h={card.h}
          tone={GROUP_TONE[card.group]}
          selected={card.name === selected}
          compact={card.group === "system"}
        />
      ))}

      {present.has("search_profiles") && present.has("scrape_runs") && (
        <g>
          <line
            x1={321}
            y1={107}
            x2={407}
            y2={107}
            style={{ stroke: "var(--sig)" }}
            strokeWidth="1.5"
            markerEnd={`url(#${markerId("db-03", "green")})`}
          />
          <text
            x={364}
            y={97}
            textAnchor="middle"
            style={{ fill: "var(--sig)", fontSize: "var(--text-fine)" }}
          >
            profile_id
          </text>
          <text
            x={364}
            y={121}
            textAnchor="middle"
            style={{ fill: "var(--faint)", fontSize: "var(--text-fine)" }}
          >
            RESTRICT
          </text>
        </g>
      )}

      {present.has("scrape_runs") && present.has("scrape_run_progress") && (
        <g>
          <line
            x1={547}
            y1={161}
            x2={547}
            y2={147}
            strokeDasharray="5 4"
            style={{ stroke: "var(--brand-3)" }}
            strokeWidth="1.3"
            markerEnd={`url(#${markerId("db-03", "violet")})`}
          />
          <text x={559} y={158} style={{ fill: "var(--brand-3)", fontSize: "var(--text-fine)" }}>
            same run id
          </text>
        </g>
      )}

      {present.has("job_postings") && present.has("scrape_runs") && (
        <g>
          <path
            d="M907,141 C 907,300 547,320 547,235"
            fill="none"
            strokeDasharray="5 5"
            style={{ stroke: "var(--amber)" }}
            strokeWidth="1.3"
            markerEnd={`url(#${markerId("db-03", "amber")})`}
          />
          <text
            x={727}
            y={312}
            textAnchor="middle"
            style={{ fill: "var(--amber)", fontSize: "var(--text-fine)" }}
          >
            LastSeenAt in [StartedAt, FinishedAt] · no FK
          </text>
        </g>
      )}

      {otherRows > 0 && (
        <g>
          <GroupBox group="system" x={30} y={otherY} w={1300} h={otherH} title="OTHER TABLES" />
          {other.map((name, index) => (
            <TableCard
              key={name}
              name={name}
              rowCount={counts[name]}
              schema={schemas[name]}
              x={50 + (index % 5) * 250}
              y={otherY + 30 + Math.floor(index / 5) * 46}
              w={240}
              h={36}
              tone="gray"
              selected={name === selected}
              compact
            />
          ))}
        </g>
      )}

      <line
        x1={30}
        y1={flowTop - 8}
        x2={ER_VIEW_W - 30}
        y2={flowTop - 8}
        strokeDasharray="3 6"
        style={{ stroke: "var(--line-hi)" }}
      />

      <text
        x={30}
        y={flowTop + 30}
        style={{ fill: "var(--amber)", fontSize: "var(--text-micro)" }}
        fontWeight="600"
        letterSpacing="0.08em"
      >
        WRITE PATH · SCRAPING RUNS INSIDE .NET
      </text>
      <text
        x={ER_VIEW_W - 30}
        y={flowTop + 30}
        textAnchor="end"
        style={{ fill: "var(--faint)", fontSize: "var(--text-fine)" }}
      >
        the BFF never talks to a source site
      </text>

      <FlowBox x={laneX[0]!} y={writeY} w={boxW} h={boxH} tone="amber" title="SOURCE SITES">
        Naukri · Indeed · LinkedIn
      </FlowBox>
      <FlowBox x={laneX[1]!} y={writeY} w={boxW} h={boxH} tone="violet" title="PLAYWRIGHT">
        headed persistent context
      </FlowBox>
      <FlowBox x={laneX[2]!} y={writeY} w={boxW} h={boxH} tone="green" title="RUN SCRAPE">
        RunScrapeHandler · quota + retry
      </FlowBox>
      <FlowBox x={laneX[3]!} y={writeY} w={boxW} h={boxH} tone="cyan" title="SQLITE">
        jobscrape.db · one writer
      </FlowBox>

      <line
        x1={laneX[0]! + boxW + 11}
        y1={writeY + 30}
        x2={laneX[1]! - 11}
        y2={writeY + 30}
        style={{ stroke: "var(--amber)" }}
        strokeWidth="1.5"
        markerStart={`url(#${startMarkerId("db-03", "amber")})`}
        markerEnd={`url(#${markerId("db-03", "amber")})`}
      />
      <FlowArrow x1={laneX[1]! + boxW} x2={laneX[2]!} y={writeY + 30} tone="violet" />
      <FlowArrow x1={laneX[2]! + boxW} x2={laneX[3]!} y={writeY + 30} tone="green" />
      <FlowLabel x={laneX[0]! + boxW + 15} y={writeY + 82}>
        navigate · extract
      </FlowLabel>
      <FlowLabel x={laneX[1]! + boxW + 15} y={writeY + 82}>
        parsed pages
      </FlowLabel>
      <FlowLabel x={laneX[2]! + boxW + 15} y={writeY + 82}>
        upsert postings
      </FlowLabel>

      <text
        x={30}
        y={readY - 20}
        style={{ fill: "var(--brand-2)", fontSize: "var(--text-micro)" }}
        fontWeight="600"
        letterSpacing="0.08em"
      >
        READ PATH · BROWSE, INSPECT, EXPORT
      </text>

      <FlowBox x={laneX[3]!} y={readY} w={boxW} h={boxH} tone="cyan" title="SQLITE">
        Dapper explorer · EF read stores
      </FlowBox>
      <FlowBox x={laneX[2]!} y={readY} w={boxW} h={boxH} tone="violet" title="API :5208">
        controllers · Problem Details
      </FlowBox>
      <FlowBox x={laneX[1]!} y={readY} w={boxW} h={boxH} tone="green" title="BFF :3000">
        same-origin /api/* proxy
      </FlowBox>
      <FlowBox x={laneX[0]!} y={readY} w={boxW} h={boxH} tone="cyan" title="OPERATOR">
        cockpit tab · CSV / XLSX
      </FlowBox>

      <FlowArrow x1={laneX[3]!} x2={laneX[2]! + boxW} y={readY + 30} tone="cyan" />
      <FlowArrow x1={laneX[2]!} x2={laneX[1]! + boxW} y={readY + 30} tone="violet" />
      <FlowArrow x1={laneX[1]!} x2={laneX[0]! + boxW} y={readY + 30} tone="green" />
      <FlowLabel x={laneX[2]! + boxW + 15} y={readY + 82}>
        rows + schema
      </FlowLabel>
      <FlowLabel x={laneX[1]! + boxW + 15} y={readY + 82}>
        JSON payload
      </FlowLabel>
      <FlowLabel x={laneX[0]! + boxW + 15} y={readY + 82}>
        stream / export
      </FlowLabel>

      <line
        x1={laneX[3]! + boxW / 2}
        y1={writeY + boxH + 5}
        x2={laneX[3]! + boxW / 2}
        y2={readY - 11}
        strokeDasharray="5 5"
        style={{ stroke: "var(--brand-2)" }}
        strokeWidth="1.3"
        markerEnd={`url(#${markerId("db-03", "cyan")})`}
      />
      <text
        x={laneX[3]! + boxW / 2 + 10}
        y={writeY + boxH + 45}
        style={{ fill: "var(--brand-2)", fontSize: "var(--text-fine)" }}
      >
        same SQLite file
      </text>
    </svg>
  );
}

function GroupBox({
  group,
  x,
  y,
  w,
  h,
  title,
}: {
  group: TableGroup;
  x: number;
  y: number;
  w: number;
  h: number;
  title?: string;
}) {
  const tone = GROUP_TONE[group];
  return (
    <g>
      <rect
        x={x}
        y={y}
        width={w}
        height={h}
        rx="10"
        strokeDasharray="6 4"
        style={{ fill: TONE[tone].fill, stroke: TONE[tone].stroke }}
        strokeWidth="1"
      />
      <text
        x={x + 12}
        y={y + 16}
        style={{ fill: TONE[tone].text, fontSize: "var(--text-fine)" }}
        fontWeight="600"
        letterSpacing="0.08em"
      >
        {title ?? GROUP_LABEL[group]}
      </text>
    </g>
  );
}

function TableCard({
  name,
  rowCount,
  schema,
  x,
  y,
  w,
  h,
  tone,
  selected,
  compact = false,
}: {
  name: string;
  rowCount?: number;
  schema?: DbTableSchema;
  x: number;
  y: number;
  w: number;
  h: number;
  tone: DiagramTone;
  selected: boolean;
  compact?: boolean;
}) {
  const max = w >= 240 ? 30 : w >= 160 ? 24 : 16;
  const meta =
    schema === undefined
      ? rowCount === undefined
        ? ""
        : rowsLabel(rowCount)
      : `${schema.columns.length} cols${rowCount === undefined ? "" : ` · ${rowsLabel(rowCount)}`}`;
  const nameY = compact ? (h < 44 ? 16 : 20) : 22;
  const metaY = compact ? (h < 44 ? h - 7 : 36) : 40;
  return (
    <g>
      <title>{name}</title>
      <rect
        className="diagram-card"
        x={x}
        y={y}
        width={w}
        height={h}
        rx="5"
        style={{
          fill: "var(--panel)",
          stroke: selected ? "var(--sig)" : "var(--line-hi)",
        }}
        strokeWidth={selected ? 2 : 1}
      />
      <rect x={x} y={y} width="3" height={h} rx="1.5" style={{ fill: TONE[tone].stroke }} />
      <text
        x={x + 12}
        y={y + nameY}
        style={{ fill: "var(--ink)", fontSize: compact ? "var(--text-fine)" : "var(--text-micro)" }}
        fontWeight={selected ? "600" : undefined}
      >
        {truncateName(name, max)}
      </text>
      {meta !== "" && (
        <text x={x + 12} y={y + metaY} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
          {meta}
        </text>
      )}
    </g>
  );
}

function FlowBox({
  x,
  y,
  w,
  h,
  tone,
  title,
  children,
}: {
  x: number;
  y: number;
  w: number;
  h: number;
  tone: DiagramTone;
  title: string;
  children: string;
}) {
  return (
    <g>
      <rect
        x={x}
        y={y}
        width={w}
        height={h}
        rx="8"
        style={{ fill: TONE[tone].fill, stroke: TONE[tone].stroke }}
        strokeWidth="1.3"
      />
      <text
        x={x + w / 2}
        y={y + 24}
        textAnchor="middle"
        style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
        fontWeight="600"
      >
        {title}
      </text>
      <text
        x={x + w / 2}
        y={y + 42}
        textAnchor="middle"
        style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
      >
        {children}
      </text>
    </g>
  );
}

function FlowArrow({ x1, x2, y, tone }: { x1: number; x2: number; y: number; tone: DiagramTone }) {
  const rightward = x2 > x1;
  const start = rightward ? x1 + 5 : x1 - 5;
  const end = rightward ? x2 - 11 : x2 + 11;
  return (
    <line
      x1={start}
      y1={y}
      x2={end}
      y2={y}
      style={{ stroke: TONE[tone].stroke }}
      strokeWidth="1.5"
      markerEnd={`url(#${markerId("db-03", tone)})`}
    />
  );
}

function FlowLabel({ x, y, children }: { x: number; y: number; children: string }) {
  return (
    <text
      x={x}
      y={y}
      textAnchor="middle"
      style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
    >
      {children}
    </text>
  );
}
