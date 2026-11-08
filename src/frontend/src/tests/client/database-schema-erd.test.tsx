import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, render } from "@testing-library/react";

import type { DbTableSchema } from "../../client/lib/api.ts";
import { SchemaErd } from "../../client/pages/database-schema-erd.tsx";

beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
});

const col = (
  name: string,
  type: string,
  notNull: boolean,
  primaryKey = false,
): DbTableSchema["columns"][number] => ({ name, type, notNull, primaryKey, defaultValue: null });

const searchProfiles: DbTableSchema = {
  name: "search_profiles",
  columns: [
    col("id", "TEXT", true, true),
    col("name", "TEXT", true),
    col("source_key", "TEXT", true),
    col("criteria", "TEXT", true),
    col("limits", "TEXT", true),
    col("enabled", "INTEGER", true),
    col("created_at", "INTEGER", true),
    col("updated_at", "INTEGER", true),
  ],
  foreignKeys: [],
  primaryKeyColumn: "id",
};

const scrapeRuns: DbTableSchema = {
  name: "scrape_runs",
  columns: [
    col("id", "TEXT", true, true),
    col("profile_id", "TEXT", true),
    col("source_key", "TEXT", true),
    col("limits", "TEXT", true),
    col("companies", "TEXT", true),
    col("stage", "TEXT", true),
    col("stats", "TEXT", true),
    col("created_at", "INTEGER", true),
    col("started_at", "INTEGER", false),
    col("finished_at", "INTEGER", false),
    col("pause_reason", "TEXT", false),
    col("retry_at", "INTEGER", false),
    col("failure_reason", "TEXT", false),
    col("resume_stage", "TEXT", false),
  ],
  foreignKeys: [
    {
      column: "profile_id",
      referencedTable: "search_profiles",
      referencedColumn: "id",
      onDelete: "RESTRICT",
    },
  ],
  primaryKeyColumn: "id",
};

const scrapeRunProgress: DbTableSchema = {
  name: "scrape_run_progress",
  columns: [
    col("id", "TEXT", true, true),
    col("pages_completed", "INTEGER", true),
    col("next_cursor", "TEXT", false),
    col("seen_external_ids", "TEXT", true),
    col("updated_at", "INTEGER", true),
  ],
  foreignKeys: [
    {
      column: "id",
      referencedTable: "scrape_runs",
      referencedColumn: "id",
      onDelete: "CASCADE",
    },
  ],
  primaryKeyColumn: "id",
};

const schemas: Record<string, DbTableSchema> = {
  search_profiles: searchProfiles,
  scrape_runs: scrapeRuns,
  scrape_run_progress: scrapeRunProgress,
};

const counts = { search_profiles: 2, scrape_runs: 7, scrape_run_progress: 7 };

function box(svg: Element, name: string): Element {
  const group = svg.querySelector(`[data-table="${name}"]`);
  if (group === null) {
    throw new Error(`box ${name} missing`);
  }
  return group;
}

describe("schema ERD", () => {
  test("draws the three-table run subsystem and counts the live tables", () => {
    const unrelated: Record<string, DbTableSchema> = {};
    for (let i = 0; i < 9; i += 1) {
      unrelated[`other_${i}`] = { ...searchProfiles, name: `other_${i}` };
    }
    const nine = { ...schemas, ...unrelated };
    const queries = render(<SchemaErd schemas={nine} selected={null} counts={counts} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });

    expect(svg.querySelectorAll("[data-table]").length).toBe(3);
    expect(queries.getByText(/3 of 12 live tables/)).toBeDefined();
  });

  test("labels both enforced edges with the actions the API reports", () => {
    const queries = render(<SchemaErd schemas={schemas} selected={null} counts={counts} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });
    const edges = Array.from(svg.querySelectorAll("path[data-fk]"));

    expect(edges.length).toBe(2);
    const restrict = edges.find(
      (edge) => edge.getAttribute("data-from") === "scrape_runs.profile_id",
    );
    expect(restrict?.getAttribute("data-to")).toBe("search_profiles.id");
    expect(restrict?.getAttribute("data-action")).toBe("RESTRICT");
    const cascade = edges.find(
      (edge) => edge.getAttribute("data-from") === "scrape_run_progress.id",
    );
    expect(cascade?.getAttribute("data-to")).toBe("scrape_runs.id");
    expect(cascade?.getAttribute("data-action")).toBe("CASCADE");
    expect(svg.textContent).toContain("ON DELETE RESTRICT");
    expect(svg.textContent).toContain("ON DELETE CASCADE");
  });

  test("caps the visible rows and overflows the rest", () => {
    const wide: Record<string, DbTableSchema> = {
      ...schemas,
      search_profiles: {
        ...searchProfiles,
        columns: [
          ...searchProfiles.columns,
          col("deleted_at", "INTEGER", false),
          col("note", "TEXT", false),
        ],
      },
    };
    const queries = render(<SchemaErd schemas={wide} selected={null} counts={counts} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });
    const profiles = box(svg, "search_profiles");

    expect(profiles.querySelectorAll("[data-row]").length).toBe(8);
    expect(profiles.querySelector("[data-overflow]")?.getAttribute("data-overflow")).toBe("2");
    expect(profiles.textContent).toContain("+ 2 more columns");
    const grownHeight = Number(profiles.querySelector("rect.erd-outline")?.getAttribute("height"));

    queries.unmount();
    const base = render(<SchemaErd schemas={schemas} selected={null} counts={counts} />);
    const baseSvg = base.getByRole("img", { name: "Column-level ERD of the run subsystem" });
    const baseHeight = Number(
      box(baseSvg, "search_profiles").querySelector("rect.erd-outline")?.getAttribute("height"),
    );
    expect(grownHeight - baseHeight).toBe(24);
  });

  test("an out-of-subsystem selection names it in the caption instead of bordering a box", () => {
    const queries = render(<SchemaErd schemas={schemas} selected="job_postings" counts={counts} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });

    for (const outline of svg.querySelectorAll("rect.erd-outline")) {
      expect(outline.getAttribute("style")).toContain("var(--line-hi)");
    }
    expect(queries.getByText(/Selected job_postings is outside this subsystem/)).toBeDefined();
  });

  test("an in-subsystem selection gets the signal border", () => {
    const queries = render(<SchemaErd schemas={schemas} selected="scrape_runs" counts={counts} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });
    const outline = box(svg, "scrape_runs").querySelector("rect.erd-outline");

    expect(outline?.getAttribute("style")).toContain("var(--sig)");
    expect(outline?.getAttribute("stroke-width")).toBe("2");
  });

  test("a payload without onDelete renders no focal accent and says actions are not exposed", () => {
    const stripped: Record<string, DbTableSchema> = {
      ...schemas,
      scrape_runs: {
        ...scrapeRuns,
        foreignKeys: scrapeRuns.foreignKeys.map((fk) => {
          const { onDelete: _, ...rest } = fk;
          return rest;
        }),
      },
      scrape_run_progress: {
        ...scrapeRunProgress,
        foreignKeys: scrapeRunProgress.foreignKeys.map((fk) => {
          const { onDelete: _, ...rest } = fk;
          return rest;
        }),
      },
    };
    const queries = render(<SchemaErd schemas={stripped} selected={null} counts={counts} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });

    expect(svg.querySelector("[style*='diagram-red-soft']")).toBeNull();
    expect(svg.querySelector("path[data-action]")).toBeNull();
    expect(svg.querySelectorAll("path[data-fk]").length).toBe(2);
    expect(queries.getByText(/does not expose ON DELETE actions/)).toBeDefined();
  });

  test("empty schemas render a compact placeholder, not a 480px void", () => {
    const queries = render(<SchemaErd schemas={{}} selected={null} counts={{}} />);
    const svg = queries.getByRole("img", { name: "Column-level ERD of the run subsystem" });

    expect(svg.getAttribute("viewBox")).toBe("0 0 360 72");
    expect(svg.querySelectorAll("[data-table]").length).toBe(0);
  });
});
