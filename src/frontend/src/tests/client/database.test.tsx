import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import type { ApiClient } from "../../client/lib/api.ts";
import { ErFlowDiagram, TablesDiagram } from "../../client/pages/database-diagram.tsx";
import {
  cellFull,
  cellText,
  coerce,
  formatTimestamp,
  isTimestampColumn,
} from "../../client/pages/database-values.ts";
import { DatabasePage } from "../../client/pages/database.tsx";

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

const schema = {
  name: "search_profiles",
  columns: [
    { name: "id", type: "TEXT", notNull: true, primaryKey: true, defaultValue: null },
    { name: "name", type: "TEXT", notNull: true, primaryKey: false, defaultValue: null },
    { name: "enabled", type: "INTEGER", notNull: false, primaryKey: false, defaultValue: null },
  ],
  foreignKeys: [],
  primaryKeyColumn: "id",
};

const rows = {
  rows: [{ id: "p1", name: "backend", enabled: 1 }],
  total: 1,
};

function stubClient(overrides: Partial<ApiClient> = {}): ApiClient {
  return {
    listDbTables: async () => ({
      result: { ok: true, value: { items: [{ name: "search_profiles", rowCount: 1 }] } },
      ms: 1,
      at: new Date().toISOString(),
    }),
    getDbSchema: async () => ({ result: { ok: true, value: schema }, ms: 1, at: "" }),
    listDbRows: async () => ({ result: { ok: true, value: rows }, ms: 1, at: "" }),
    insertDbRow: async () => ({ result: { ok: true, value: null }, ms: 1, at: "" }),
    updateDbRow: async () => ({ result: { ok: true, value: null }, ms: 1, at: "" }),
    deleteDbRow: async () => ({ result: { ok: true, value: null }, ms: 1, at: "" }),
    ...overrides,
  } as unknown as ApiClient;
}

describe("database page", () => {
  test("tables, schema, and rows render from the API", async () => {
    const queries = render(<DatabasePage client={stubClient()} />);

    await waitFor(() => expect(queries.getAllByText("search_profiles").length).toBeGreaterThan(0));
    await waitFor(() => expect(queries.getByText("backend")).toBeDefined());
    expect(queries.getByRole("table", { name: "Database tables" })).toBeDefined();
    expect(queries.getByRole("table", { name: "search_profiles schema" })).toBeDefined();
  });

  test("delete arms first and deletes on the second click", async () => {
    let calls = 0;
    const client = stubClient({
      deleteDbRow: async () => {
        calls += 1;
        return { result: { ok: true, value: null }, ms: 1, at: "" };
      },
    });
    const queries = render(<DatabasePage client={client} />);
    await waitFor(() => expect(queries.getByText("backend")).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "Delete row p1" }));
    expect(queries.getByRole("button", { name: "Confirm delete row p1" })).toBeDefined();
    expect(calls).toBe(0);

    fireEvent.click(queries.getByRole("button", { name: "Confirm delete row p1" }));
    await waitFor(() => expect(calls).toBe(1));
  });

  test("insert posts coerced values for the visible columns", async () => {
    let seen: Record<string, unknown> = {};
    const client = stubClient({
      insertDbRow: async (_table, values) => {
        seen = values;
        return { result: { ok: true, value: null }, ms: 1, at: "" };
      },
    });
    const queries = render(<DatabasePage client={client} />);
    await waitFor(() => expect(queries.getByText("backend")).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "NEW ROW" }));
    const pairs: Array<[string, string]> = [
      ["id", "p2"],
      ["name", "ml"],
      ["enabled", "1"],
    ];
    for (const [name, value] of pairs) {
      fireEvent.change(queries.getByLabelText(name), { target: { value } });
    }
    fireEvent.click(queries.getByRole("button", { name: "SAVE ROW" }));

    await waitFor(() => expect(seen).toEqual({ id: "p2", name: "ml", enabled: 1 }));
  });

  test("update sends the edited row under its original pk", async () => {
    let seenPk = "";
    let seenValues: Record<string, unknown> = {};
    const client = stubClient({
      updateDbRow: async (_table, pk, values) => {
        seenPk = pk;
        seenValues = values;
        return { result: { ok: true, value: null }, ms: 1, at: "" };
      },
    });
    const queries = render(<DatabasePage client={client} />);
    await waitFor(() => expect(queries.getByText("backend")).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "Edit row p1" }));
    fireEvent.change(queries.getByLabelText("name in row p1"), { target: { value: "ml" } });
    fireEvent.click(queries.getByRole("button", { name: "SAVE" }));

    await waitFor(() => expect(seenPk).toBe("p1"));
    expect(seenValues).toEqual({ name: "ml", enabled: 1 });
  });

  test("edit opens the row without submitting it", async () => {
    let calls = 0;
    const client = stubClient({
      updateDbRow: async () => {
        calls += 1;
        return { result: { ok: true, value: null }, ms: 1, at: "" };
      },
    });
    const queries = render(<DatabasePage client={client} />);
    await waitFor(() => expect(queries.getByText("backend")).toBeDefined());

    const edit = queries.getByRole("button", { name: "Edit row p1" });
    fireEvent.click(edit);
    expect(queries.getByLabelText("name in row p1")).toBeDefined();
    expect(queries.getByRole("button", { name: "SAVE" }).getAttribute("type")).toBe("button");
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(calls).toBe(0);
  });

  test("timestamp columns render as UTC, not raw ticks", async () => {
    const timestampSchema = {
      ...schema,
      columns: [
        ...schema.columns,
        {
          name: "created_at",
          type: "INTEGER",
          notNull: true,
          primaryKey: false,
          defaultValue: null,
        },
      ],
    };
    const client = stubClient({
      getDbSchema: async () => ({ result: { ok: true, value: timestampSchema }, ms: 1, at: "" }),
      listDbRows: async () => ({
        result: {
          ok: true,
          value: {
            rows: [{ id: "p1", name: "backend", enabled: 1, created_at: "639292351661360320" }],
            total: 1,
          },
        },
        ms: 1,
        at: "",
      }),
    });
    const queries = render(<DatabasePage client={client} />);
    await waitFor(() => expect(queries.getByText("2026-11-02 16:52:46 UTC")).toBeDefined());
    expect(queries.getByText("2026-11-02 16:52:46 UTC").getAttribute("title")).toContain(
      "639292351661360320 ticks",
    );
  });

  test("empty insert warns without calling the API", async () => {
    let calls = 0;
    const client = stubClient({
      insertDbRow: async () => {
        calls += 1;
        return { result: { ok: true, value: null }, ms: 1, at: "" };
      },
    });
    const queries = render(<DatabasePage client={client} />);
    await waitFor(() => expect(queries.getByText("backend")).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "NEW ROW" }));
    fireEvent.click(queries.getByRole("button", { name: "SAVE ROW" }));

    await waitFor(() => expect(queries.getByText("Fill at least one column.")).toBeDefined());
    expect(calls).toBe(0);
  });

  test("problems render with their stable code", async () => {
    const client = stubClient({
      listDbTables: async () => ({
        result: {
          ok: false,
          problem: {
            status: 502,
            title: "Down",
            detail: "unreachable",
            code: "DOTNET_UNREACHABLE",
          },
        },
        ms: 1,
        at: "",
      }),
    });
    const queries = render(<DatabasePage client={client} />);

    await waitFor(() => expect(queries.getByText("DOTNET_UNREACHABLE")).toBeDefined());
  });
});

describe("database helpers", () => {
  test("coerce reads numbers and booleans from the column type", () => {
    const int = {
      name: "n",
      type: "INTEGER",
      notNull: true,
      primaryKey: false,
      defaultValue: null,
    };
    const flag = { name: "f", type: "BOOL", notNull: false, primaryKey: false, defaultValue: null };
    expect(coerce(int, "42")).toBe(42);
    expect(coerce(int, "639292351661360320")).toBe("639292351661360320");
    expect(coerce(int, "nope")).toBe("nope");
    expect(coerce(flag, "true")).toBe(true);
    expect(coerce(flag, "")).toBe(null);
  });

  test("cellText trims long values", () => {
    expect(cellText("short")).toBe("short");
    expect(cellText(null)).toBe("");
    expect(cellText("x".repeat(200)).endsWith("…")).toBe(true);
  });

  test("timestamp helpers read .NET ticks and ignore lookalikes", () => {
    expect(isTimestampColumn("created_at")).toBe(true);
    expect(isTimestampColumn("cooldown_until")).toBe(true);
    expect(isTimestampColumn("pages_fetched")).toBe(false);
    expect(formatTimestamp("639292351661360320")).toBe("2026-11-02 16:52:46 UTC");
    expect(formatTimestamp(null)).toBeNull();
    expect(formatTimestamp("nope")).toBeNull();
    expect(formatTimestamp(42)).toBeNull();
    expect(cellText("639292351661360320", "created_at")).toBe("2026-11-02 16:52:46 UTC");
    expect(cellText("639292351661360320", "pages_fetched")).toBe("639292351661360320");
    expect(cellFull("639292351661360320", "created_at")).toContain("639292351661360320 ticks");
  });

  test("the diagram draws one node per live table", () => {
    const queries = render(<ErFlowDiagram tables={["a", "b"]} schemas={{}} />);
    const svg = queries.getByRole("img", { name: "Entity relationships and data flow" });
    expect(svg.textContent).toContain("a");
    expect(svg.textContent).toContain("b");
    expect(svg.textContent).toContain("WRITE PATH");
    expect(svg.textContent).toContain("READ PATH");
  });

  test("the diagram marks the selected table with the signal border", () => {
    const queries = render(<ErFlowDiagram tables={["a", "b"]} schemas={{}} selected="b" />);
    const svg = queries.getByRole("img", { name: "Entity relationships and data flow" });
    const group = (name: string) =>
      Array.from(svg.querySelectorAll("g")).find(
        (g) => g.querySelector("title")?.textContent === name,
      );
    expect(group("b")?.querySelector("rect.diagram-card")?.getAttribute("style")).toContain(
      "var(--sig)",
    );
    expect(group("a")?.querySelector("rect.diagram-card")?.getAttribute("style")).toContain(
      "var(--line-hi)",
    );
  });

  test("the tables map shows names, counts, and the selected table", () => {
    const queries = render(
      <TablesDiagram
        tables={[
          { name: "a", rowCount: 1 },
          { name: "b", rowCount: 5 },
        ]}
        selected="b"
      />,
    );
    const svg = queries.getByRole("img", { name: "Tables map" });
    expect(svg.textContent).toContain("a");
    expect(svg.textContent).toContain("1 row");
    expect(svg.textContent).toContain("5 rows");
    const cards = svg.querySelectorAll("rect.diagram-card");
    expect(cards.length).toBe(2);
    expect(cards[1]?.getAttribute("style")).toContain("var(--sig)");
    expect(cards[0]?.getAttribute("style")).toContain("var(--line-hi)");
  });

  test("the tables map truncates long names but keeps the full name", () => {
    const long = `table_${"x".repeat(30)}`;
    const queries = render(
      <TablesDiagram tables={[{ name: long, rowCount: 0 }]} selected={null} />,
    );
    const svg = queries.getByRole("img", { name: "Tables map" });
    expect(svg.textContent).toContain("…");
    expect(svg.querySelector("title")?.textContent).toBe(long);
  });
});
