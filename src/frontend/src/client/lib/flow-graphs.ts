import type { Edge, Node } from "@xyflow/react";
import type { CSSProperties } from "react";

import type { DbTableSchema } from "./api.ts";

const COLUMNS = 3;
const GRID_GAP_X = 240;
const GRID_GAP_Y = 56;
const GRID_ORIGIN_X = 20;
const GRID_ORIGIN_Y = 20;
const NODE_WIDTH = 200;
const NODE_HEIGHT = 30;

const LANE_Y_OFFSET = 74;
const LANE_HEIGHT = 28;
const LANE_WIDTH = 140;

const SIG = "var(--sig)";
const RED = "var(--red)";
const AMBER = "var(--amber)";
const DIM = "var(--dim)";

function gridPosition(index: number): { x: number; y: number } {
  return {
    x: GRID_ORIGIN_X + (index % COLUMNS) * GRID_GAP_X,
    y: GRID_ORIGIN_Y + Math.floor(index / COLUMNS) * GRID_GAP_Y,
  };
}

function tableNode(id: string, index: number, extraStyle?: CSSProperties): Node {
  return {
    id,
    position: gridPosition(index),
    data: { label: id },
    style: {
      width: NODE_WIDTH,
      height: NODE_HEIGHT,
      ...extraStyle,
    },
  };
}

function sigEdge(id: string, source: string, target: string, label: string): Edge {
  return {
    id,
    source,
    target,
    label,
    animated: true,
    style: { stroke: SIG },
  };
}

/**
 * Entity graph from live schemas: one node per table on the shared 3-col grid, one solid sig edge
 * per foreign key whose target exists, one dashed dim bus edge per table carrying a source_key
 * column, then the fixed DATA IN AND OUT lane with amber out-edges to the postings window and the
 * export nodes.
 */
export function buildErNodesEdges(
  tables: string[],
  schemas: Record<string, DbTableSchema>,
): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = tables.map((t, i) => tableNode(t, i));
  const edges: Edge[] = [];

  tables.forEach((t) => {
    for (const fk of schemas[t]?.foreignKeys ?? []) {
      if (!tables.includes(fk.referencedTable)) {
        continue;
      }
      edges.push({
        id: `fk-${t}-${fk.column}`,
        source: t,
        target: fk.referencedTable,
        label: fk.column,
        style: { stroke: SIG },
      });
    }
  });

  const gridBottom = GRID_ORIGIN_Y + Math.ceil(Math.max(tables.length, 1) / COLUMNS) * GRID_GAP_Y;
  const laneY = gridBottom + LANE_Y_OFFSET;
  const sourceKeyTables = tables.filter((t) =>
    (schemas[t]?.columns ?? []).some((c) => c.name === "source_key"),
  );
  if (sourceKeyTables.length > 0) {
    nodes.push({
      id: "source-key-bus",
      position: { x: GRID_ORIGIN_X, y: gridBottom + 16 },
      data: { label: "source_key shared value, no FK" },
      style: { width: COLUMNS * GRID_GAP_X - 40, height: 24 },
    });
    for (const t of sourceKeyTables) {
      edges.push({
        id: `bus-${t}`,
        source: t,
        target: "source-key-bus",
        label: "source_key",
        style: { stroke: DIM, strokeDasharray: "4 4" },
      });
    }
  }

  const lane: Array<[string, string]> = [
    ["lane-source", "SOURCE SITES"],
    ["lane-bff", "BFF :3000"],
    ["lane-dotnet", ".NET :5208"],
    ["lane-store", "SQLITE STORE"],
  ];
  nodes.push({
    id: "data-in-out-title",
    position: { x: GRID_ORIGIN_X, y: laneY - 30 },
    data: { label: "DATA IN AND OUT" },
    style: { width: 200, height: 20, background: "transparent", border: "none" },
  });
  lane.forEach(([id, label], i) => {
    nodes.push({
      id,
      position: { x: GRID_ORIGIN_X + i * 160, y: laneY },
      data: { label },
      style: { width: LANE_WIDTH, height: LANE_HEIGHT },
    });
  });
  nodes.push({
    id: "run-postings-window",
    position: { x: GRID_ORIGIN_X + 410, y: laneY + 70 },
    data: { label: "run postings window" },
    style: { width: LANE_WIDTH, height: LANE_HEIGHT },
  });
  nodes.push({
    id: "csv-xlsx",
    position: { x: GRID_ORIGIN_X + 590, y: laneY + 70 },
    data: { label: "csv / xlsx" },
    style: { width: LANE_WIDTH, height: LANE_HEIGHT },
  });

  edges.push(
    {
      id: "flow-scrape",
      source: "lane-source",
      target: "lane-bff",
      label: "scrape",
      animated: true,
      style: { stroke: AMBER },
    },
    {
      id: "flow-proxy",
      source: "lane-bff",
      target: "lane-dotnet",
      label: "proxy",
      animated: true,
      style: { stroke: AMBER },
    },
    {
      id: "flow-upsert",
      source: "lane-dotnet",
      target: "lane-store",
      label: "upsert",
      animated: true,
      style: { stroke: AMBER },
    },
    {
      id: "flow-window",
      source: "lane-store",
      target: "run-postings-window",
      label: "LastSeenAt in [start, finish]",
      animated: true,
      style: { stroke: AMBER },
    },
    {
      id: "flow-export",
      source: "lane-store",
      target: "csv-xlsx",
      label: "export endpoints",
      animated: true,
      style: { stroke: AMBER },
    },
  );

  return { nodes, edges };
}

/**
 * Request lifecycle: four stops across the same-origin proxy, six sig edges for the happy path, one
 * red miss edge, one amber self-loop for the down plane.
 */
export function buildLifecycleNodesEdges(): { nodes: Node[]; edges: Edge[] } {
  const stops = ["BROWSER", "BFF :3000", ".NET :5208", "STORE"];
  const nodes: Node[] = stops.map((label, i) => ({
    id: label,
    position: { x: GRID_ORIGIN_X + i * 200, y: GRID_ORIGIN_Y },
    data: { label },
    style: { width: 140, height: NODE_HEIGHT },
  }));
  const edges: Edge[] = [
    sigEdge("life-1", "BROWSER", "BFF :3000", "GET /api/v1/…"),
    sigEdge("life-2", "BFF :3000", ".NET :5208", "proxy → loopback"),
    sigEdge("life-3", ".NET :5208", "STORE", "GetAsync(id)"),
    sigEdge("life-4", "STORE", ".NET :5208", "hit? profile : null"),
    sigEdge("life-5", ".NET :5208", "BFF :3000", "200 · payload"),
    sigEdge("life-6", "BFF :3000", "BROWSER", "stream back"),
    {
      id: "life-miss",
      source: ".NET :5208",
      target: "BFF :3000",
      label: "404 + PROFILE_NOT_FOUND",
      style: { stroke: RED },
    },
    {
      id: "life-down",
      source: "BFF :3000",
      target: "BFF :3000",
      label: "API down → 502 DOTNET_UNREACHABLE",
      style: { stroke: AMBER },
    },
  ];
  return { nodes, edges };
}

/** Tables overview: one modest node per table, sig border on the selected one, no edges. */
export function buildTablesNodesEdges(
  tables: Array<{ name: string; rowCount: number }>,
  selected: string | null,
): { nodes: Node[]; edges: Edge[] } {
  const nodes: Node[] = tables.map((t, i) => ({
    id: t.name,
    position: {
      x: GRID_ORIGIN_X + (i % COLUMNS) * 200,
      y: GRID_ORIGIN_Y + Math.floor(i / COLUMNS) * 64,
    },
    data: { label: `${t.name} (${t.rowCount})` },
    style: {
      width: 160,
      height: 36,
      ...(t.name === selected ? { border: `1px solid ${SIG}` } : {}),
    },
  }));
  return { nodes, edges: [] };
}
