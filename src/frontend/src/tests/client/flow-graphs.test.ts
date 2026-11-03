import { getBezierPath, Position } from "@xyflow/react";
import { describe, expect, test } from "bun:test";

import type { DbTableSchema } from "../../client/lib/api.ts";
import {
  buildErNodesEdges,
  buildLifecycleNodesEdges,
  buildTablesNodesEdges,
} from "../../client/lib/flow-graphs.ts";

function schemaWith(
  columns: string[],
  foreignKeys: Array<{ column: string; referencedTable: string }>,
): DbTableSchema {
  return {
    name: "t",
    columns: columns.map((name) => ({
      name,
      type: "TEXT",
      notNull: false,
      primaryKey: false,
      defaultValue: null,
    })),
    foreignKeys: foreignKeys.map((fk) => ({ ...fk, referencedColumn: "id" })),
    primaryKeyColumn: "id",
  };
}

describe("buildErNodesEdges", () => {
  test("one node per table plus the fixed lane nodes", () => {
    const { nodes } = buildErNodesEdges(["a", "b"], {});
    const ids = nodes.map((n) => n.id);
    expect(ids).toContain("a");
    expect(ids).toContain("b");
    for (const lane of ["lane-source", "lane-bff", "lane-dotnet", "lane-store"]) {
      expect(ids).toContain(lane);
    }
    expect(ids).toContain("run-postings-window");
    expect(ids).toContain("csv-xlsx");
  });

  test("one sig edge per FK, skipping missing targets", () => {
    const schemas = {
      a: schemaWith(
        ["id"],
        [
          { column: "profile_id", referencedTable: "b" },
          { column: "ghost_id", referencedTable: "missing" },
        ],
      ),
      b: schemaWith(["id"], []),
    };
    const { edges } = buildErNodesEdges(["a", "b"], schemas);
    const fk = edges.filter((e) => e.id.startsWith("fk-"));
    expect(fk).toHaveLength(1);
    expect(fk[0]?.source).toBe("a");
    expect(fk[0]?.target).toBe("b");
    expect(fk[0]?.label).toBe("profile_id");
    expect((fk[0]?.style as Record<string, string> | undefined)?.["stroke"]).toBe("var(--sig)");
  });

  test("one dashed dim bus edge per source_key table", () => {
    const schemas = {
      a: schemaWith(["id", "source_key"], []),
      b: schemaWith(["id"], []),
    };
    const { edges } = buildErNodesEdges(["a", "b"], schemas);
    const bus = edges.filter((e) => e.id.startsWith("bus-"));
    expect(bus).toHaveLength(1);
    expect(bus[0]?.source).toBe("a");
    const style = bus[0]?.style as Record<string, string> | undefined;
    expect(style?.["stroke"]).toBe("var(--dim)");
    expect(style?.["strokeDasharray"]).toBe("4 4");
  });

  test("amber out-edges reach the postings window and export nodes", () => {
    const { edges } = buildErNodesEdges(["a"], {});
    const amber = edges.filter(
      (e) => (e.style as Record<string, string> | undefined)?.["stroke"] === "var(--amber)",
    );
    const targets = amber.map((e) => e.target);
    expect(targets).toContain("run-postings-window");
    expect(targets).toContain("csv-xlsx");
  });
});

describe("buildLifecycleNodesEdges", () => {
  test("four stops, six sig edges, one red miss, one amber self-loop", () => {
    const { nodes, edges } = buildLifecycleNodesEdges();
    expect(nodes.map((n) => n.id)).toEqual(["BROWSER", "BFF :3000", ".NET :5208", "STORE"]);
    const stroke = (id: string) =>
      (edges.find((e) => e.id === id)?.style as Record<string, string> | undefined)?.["stroke"];
    const sig = edges.filter(
      (e) => e.id.startsWith("life-") && !["life-miss", "life-down"].includes(e.id),
    );
    expect(sig).toHaveLength(6);
    expect(sig.every((e) => (e.style as Record<string, string>)["stroke"] === "var(--sig)")).toBe(
      true,
    );
    expect(stroke("life-miss")).toBe("var(--red)");
    expect(edges.find((e) => e.id === "life-miss")?.label).toBe("404 + PROFILE_NOT_FOUND");
    expect(stroke("life-down")).toBe("var(--amber)");
    const loop = edges.find((e) => e.id === "life-down");
    expect(loop?.source).toBe(loop?.target);
    expect(loop?.label).toContain("502 DOTNET_UNREACHABLE");
  });

  test("the plane-down self-loop draws a real curve, not a dot", () => {
    const { nodes } = buildLifecycleNodesEdges();
    const bff = nodes.find((n) => n.id === "BFF :3000");
    const x = Number(bff?.position.x ?? NaN);
    const y = Number(bff?.position.y ?? NaN);
    const width = Number((bff?.style as { width?: unknown } | undefined)?.width ?? NaN);
    const height = Number((bff?.style as { height?: unknown } | undefined)?.height ?? NaN);
    // React Flow hangs edges on handles: source bottom-center, target top-center.
    const [path] = getBezierPath({
      sourceX: x + width / 2,
      sourceY: y + height,
      sourcePosition: Position.Bottom,
      targetX: x + width / 2,
      targetY: y,
      targetPosition: Position.Top,
    });
    expect(path).not.toContain("NaN");
    expect(path.startsWith(`M${x + width / 2},${y + height}`)).toBe(true);
    expect(path.endsWith(`${x + width / 2},${y}`)).toBe(true);
  });
});

describe("buildTablesNodesEdges", () => {
  test("one node per table, no edges, sig border on the selected node", () => {
    const tables = [
      { name: "a", rowCount: 1 },
      { name: "b", rowCount: 2 },
    ];
    const { nodes, edges } = buildTablesNodesEdges(tables, "b");
    expect(nodes.map((n) => n.id)).toEqual(["a", "b"]);
    expect(edges).toHaveLength(0);
    const border = (id: string) =>
      (nodes.find((n) => n.id === id)?.style as Record<string, string> | undefined)?.["border"];
    expect(border("b")).toContain("var(--sig)");
    expect(border("a") ?? "").not.toContain("var(--sig)");

    const none = buildTablesNodesEdges(tables, null);
    expect(
      none.nodes.every((n) =>
        ((n.style as Record<string, string> | undefined)?.["border"] ?? "").includes("var(--sig)"),
      ),
    ).toBe(false);
  });
});
