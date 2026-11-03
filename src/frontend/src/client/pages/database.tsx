import { useEffect, useRef, useState } from "react";

import { Abbr, Glossary, Legend, Wire } from "../components/legend.tsx";
import { CodeTag, Field, Module, plural } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiClient, ApiProblem, DbRowsResult, DbTableSchema } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import { ErFlowDiagram, TablesDiagram } from "./database-diagram.tsx";
import { cellFull, cellText, readRowForm, readValue } from "./database-values.ts";

/** Rows per page in the data grid. The API clamps to 1-500; 25 keeps cells readable. */
const LIMIT = 25;

/**
 * Database page: browse every user table in the SQLite store, inspect its live schema, page
 * through rows, and run CRUD. Tables and foreign keys come from the API, so the diagram below
 * reflects the real database instead of a checked-in picture.
 */
export function DatabasePage({ client }: { client: ApiClient }) {
  useRenderLog("DatabasePage");
  const [tables, setTables] = useState<Array<{ name: string; rowCount: number }>>([]);
  const [tablesProblem, setTablesProblem] = useState<ApiProblem | null>(null);
  const [tablesLoading, setTablesLoading] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const [schemas, setSchemas] = useState<Record<string, DbTableSchema>>({});
  const [schemaProblem, setSchemaProblem] = useState<ApiProblem | null>(null);
  const [rows, setRows] = useState<DbRowsResult | null>(null);
  const [rowsProblem, setRowsProblem] = useState<ApiProblem | null>(null);
  const [rowsLoading, setRowsLoading] = useState(false);
  const [offset, setOffset] = useState(0);
  const [editingPk, setEditingPk] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [armed, setArmed] = useState<string | null>(null);
  const [notice, setNotice] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  // Latest-wins per list: a slow answer never overwrites a newer request.
  const tickets = useRef({ tables: 0, rows: 0 });
  // Schema cache mirrors `schemas` without re-rendering; the rows effect reads
  // it so selecting a known table skips a second schema round trip.
  const schemasRef = useRef<Record<string, DbTableSchema>>({});

  const schema = selected !== null ? (schemas[selected] ?? null) : null;

  function rememberSchemas(next: Record<string, DbTableSchema>) {
    schemasRef.current = { ...schemasRef.current, ...next };
    setSchemas((current) => ({ ...current, ...next }));
  }

  // The table list, shared by the first visit, the refresh button, and writes.
  // Returns the items so the first visit can chain the schema sweep.
  async function loadTables(): Promise<Array<{ name: string; rowCount: number }> | null> {
    const ticket = ++tickets.current.tables;
    setTablesLoading(true);
    try {
      const { result } = await client.listDbTables();
      if (ticket !== tickets.current.tables) {
        return null;
      }
      if (!result.ok) {
        setTablesProblem(result.problem);
        return null;
      }
      setTables(result.value.items);
      setTablesProblem(null);
      setSelected((current) => current ?? result.value.items[0]?.name ?? null);
      return result.value.items;
    } finally {
      if (ticket === tickets.current.tables) {
        setTablesLoading(false);
      }
    }
  }

  const loadTablesRef = useRef(loadTables);
  loadTablesRef.current = loadTables;

  // Once per visit: the table list, then every table's schema for the ER lane.
  useEffect(() => {
    let alive = true;
    void (async () => {
      const items = await loadTablesRef.current();
      if (!alive || items === null) {
        return;
      }
      const settled = await Promise.all(
        items.map(async (t) => {
          const s = await client.getDbSchema(t.name);
          return s.result.ok ? ([t.name, s.result.value] as const) : null;
        }),
      );
      if (!alive) {
        return;
      }
      const next: Record<string, DbTableSchema> = {};
      for (const entry of settled) {
        if (entry !== null) {
          next[entry[0]] = entry[1];
        }
      }
      rememberSchemas(next);
    })();
    return () => {
      alive = false;
    };
  }, [client]);

  // The selected table's rows, re-read on table or page change. The schema
  // comes from the cache when the sweep already fetched it.
  useEffect(() => {
    if (selected === null) {
      return;
    }
    let alive = true;
    const ticket = ++tickets.current.rows;
    setRowsLoading(true);
    void (async () => {
      try {
        const cached = schemasRef.current[selected];
        if (cached === undefined) {
          const s = await client.getDbSchema(selected);
          if (!alive || ticket !== tickets.current.rows) {
            return;
          }
          const schemaResult = s.result;
          if (schemaResult.ok) {
            rememberSchemas({ [selected]: schemaResult.value });
            setSchemaProblem(null);
          } else {
            setSchemaProblem(schemaResult.problem);
          }
        } else {
          setSchemaProblem(null);
        }
        const rr = await client.listDbRows(selected, offset, LIMIT);
        if (!alive || ticket !== tickets.current.rows) {
          return;
        }
        if (rr.result.ok) {
          setRows(rr.result.value);
          setRowsProblem(null);
        } else {
          setRowsProblem(rr.result.problem);
        }
      } finally {
        if (alive && ticket === tickets.current.rows) {
          setRowsLoading(false);
        }
      }
    })();
    return () => {
      alive = false;
    };
  }, [client, selected, offset]);

  async function refreshRows() {
    if (selected === null) {
      return;
    }
    const ticket = ++tickets.current.rows;
    const rr = await client.listDbRows(selected, offset, LIMIT);
    if (ticket !== tickets.current.rows) {
      return;
    }
    if (rr.result.ok) {
      setRows(rr.result.value);
      setRowsProblem(null);
    } else {
      setRowsProblem(rr.result.problem);
    }
  }

  async function handleInsert(form: HTMLFormElement) {
    if (selected === null || schema === null) {
      return;
    }
    setBusy("insert");
    try {
      const values = readRowForm(new FormData(form), schema);
      if (Object.keys(values).length === 0) {
        setNotice({ tone: "bad", text: "Fill at least one column." });
        return;
      }
      const { result } = await client.insertDbRow(selected, values);
      if (result.ok) {
        setNotice({ tone: "ok", text: `Inserted 1 row into ${selected}.` });
        setCreating(false);
        await refreshRows();
        await loadTables();
      } else {
        setNotice({ tone: "bad", text: `${result.problem.code}: ${result.problem.detail}` });
      }
    } finally {
      setBusy(null);
    }
  }

  async function handleUpdate(form: HTMLFormElement, pk: string) {
    if (selected === null || schema === null) {
      return;
    }
    setBusy(`update:${pk}`);
    try {
      const data = new FormData(form);
      const values: Record<string, unknown> = {};
      for (const column of schema.columns) {
        if (column.name === schema.primaryKeyColumn) {
          continue;
        }
        values[column.name] = readValue(data.get(column.name), column);
      }
      const { result } = await client.updateDbRow(selected, pk, values);
      if (result.ok) {
        setNotice({ tone: "ok", text: `Updated row ${pk}.` });
        setEditingPk(null);
        await refreshRows();
      } else {
        setNotice({ tone: "bad", text: `${result.problem.code}: ${result.problem.detail}` });
      }
    } finally {
      setBusy(null);
    }
  }

  async function handleDelete(pk: string) {
    if (selected === null) {
      return;
    }
    if (armed !== pk) {
      setArmed(pk);
      return;
    }
    setArmed(null);
    setBusy(`delete:${pk}`);
    try {
      const { result } = await client.deleteDbRow(selected, pk);
      if (result.ok) {
        setNotice({ tone: "ok", text: `Deleted row ${pk}.` });
        await refreshRows();
        await loadTables();
      } else {
        setNotice({ tone: "bad", text: `${result.problem.code}: ${result.problem.detail}` });
      }
    } finally {
      setBusy(null);
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // DATABASE" />
        </h1>
        <p>
          Every user table in the SQLite store, its live schema, and its rows. Reads are free;
          writes hit the tables directly, so they deserve a second click.
        </p>
      </div>

      <Module
        code="DB-00"
        title="Tables"
        span="span-4"
        note={`${plural(tables.length, "table")} · GET /api/v1/db/tables`}
      >
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => void loadTables()}
          disabled={tablesLoading}
        >
          {tablesLoading ? "LOADING…" : "REFRESH"}
        </button>
        {tablesProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={tablesProblem.code} tone="warn" /> {tablesProblem.title}
          </p>
        )}
        <TablesDiagram tables={tables} selected={selected} />
        <div className="tbl-wrap">
          <table className="data" aria-label="Database tables">
            <thead>
              <tr>
                <th>Table</th>
                <th>Rows</th>
              </tr>
            </thead>
            <tbody>
              {tables.map((t) => (
                <tr key={t.name} aria-current={t.name === selected ? "true" : undefined}>
                  <td>
                    <button
                      type="button"
                      className="btn btn-ghost"
                      onClick={() => {
                        setOffset(0);
                        setEditingPk(null);
                        setCreating(false);
                        setArmed(null);
                        setNotice(null);
                        setSelected(t.name);
                      }}
                    >
                      {t.name}
                    </button>
                  </td>
                  <td>{t.rowCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Module>

      <Module
        code="DB-01"
        title={selected !== null ? selected : "Schema"}
        span="span-8"
        note="Columns, types, and keys from PRAGMA table_info."
      >
        {schemaProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={schemaProblem.code} tone="warn" /> {schemaProblem.title}
          </p>
        )}
        {schema !== null && (
          <>
            <div className="tag-row" aria-label={`${schema.name} keys`}>
              <CodeTag code={`PK ${schema.primaryKeyColumn}`} />
              <CodeTag code={plural(schema.columns.length, "column")} />
              <CodeTag code={plural(schema.foreignKeys.length, "foreign key")} />
            </div>
            <div className="tbl-wrap">
              <table className="data" aria-label={`${schema.name} schema`}>
                <thead>
                  <tr>
                    <th>Column</th>
                    <th>Type</th>
                    <th>
                      <Abbr term="NN" full="Not null: the column rejects NULL" />
                    </th>
                    <th>
                      <Abbr term="PK" full="Primary key: the row identity CRUD uses" />
                    </th>
                    <th>Default</th>
                  </tr>
                </thead>
                <tbody>
                  {schema.columns.map((c) => (
                    <tr key={c.name}>
                      <td>{c.name}</td>
                      <td>{c.type || "—"}</td>
                      <td>{c.notNull ? "yes" : ""}</td>
                      <td>{c.primaryKey ? "yes" : ""}</td>
                      <td>{c.defaultValue ?? ""}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {schema.foreignKeys.length > 0 && (
              <div className="tbl-wrap">
                <table className="data" aria-label={`${schema.name} foreign keys`}>
                  <thead>
                    <tr>
                      <th>Column</th>
                      <th>References</th>
                    </tr>
                  </thead>
                  <tbody>
                    {schema.foreignKeys.map((fk, i) => (
                      <tr key={`${fk.column}-${i}`}>
                        <td>{fk.column}</td>
                        <td>
                          {fk.referencedTable}.{fk.referencedColumn}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </Module>

      <Module
        code="DB-02"
        title="Rows"
        span="span-12"
        note={
          selected === null
            ? "Pick a table."
            : `${rows === null ? "…" : plural(rows.total, "row")} in ${selected} · page ${rows === null || rows.total === 0 ? "0" : `${offset + 1}–${Math.min(offset + LIMIT, rows.total)}`}`
        }
      >
        {rowsProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={rowsProblem.code} tone="warn" /> {rowsProblem.title}
          </p>
        )}
        {notice !== null && (
          <p className="status-line" role="status">
            <CodeTag code={notice.tone === "ok" ? "SAVED" : "NOT SAVED"} tone={notice.tone} />{" "}
            {notice.text}
          </p>
        )}
        {selected !== null && schema !== null && (
          <>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => setCreating((v) => !v)}
              disabled={rowsLoading}
            >
              {creating ? "CLOSE FORM" : "NEW ROW"}
            </button>
            {creating && (
              <form
                aria-label={`New row in ${selected}`}
                noValidate
                onSubmit={(e) => {
                  e.preventDefault();
                  void handleInsert(e.currentTarget);
                }}
              >
                <div className="tbl-wrap">
                  <table className="data" aria-label={`New row in ${selected}`}>
                    <tbody>
                      {schema.columns.map((c) => (
                        <tr key={c.name}>
                          <th>
                            {c.name}
                            {c.primaryKey ? " · PK" : ""}
                            {c.notNull ? " · not null" : ""}
                          </th>
                          <td>
                            <Field label={c.name} name={c.name} errors={{}}>
                              <input
                                className="input"
                                name={c.name}
                                aria-label={c.name}
                                placeholder={c.type || "value"}
                                defaultValue=""
                              />
                            </Field>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <button type="submit" className="btn" disabled={busy === "insert"}>
                  {busy === "insert" ? "SAVING…" : "SAVE ROW"}
                </button>
              </form>
            )}
            {rows !== null && (
              <>
                <form
                  aria-label={`${selected} rows`}
                  noValidate
                  onSubmit={(e) => {
                    e.preventDefault();
                    if (editingPk !== null) {
                      void handleUpdate(e.currentTarget, editingPk);
                    }
                  }}
                >
                  <div className="tbl-wrap">
                    <table className="data" aria-label={`${selected} rows`}>
                      <thead>
                        <tr>
                          {schema.columns.map((c) => (
                            <th key={c.name}>{c.name}</th>
                          ))}
                          <th>Actions</th>
                        </tr>
                      </thead>
                      <tbody>
                        {rows.rows.map((row, i) => {
                          const pk = String(row[schema.primaryKeyColumn] ?? "");
                          const isOpen = editingPk !== null && editingPk === pk;
                          return (
                            <tr key={pk === "" ? `row-${i}` : pk}>
                              {schema.columns.map((c) => (
                                <td key={c.name} title={cellFull(row[c.name])}>
                                  {isOpen && c.name !== schema.primaryKeyColumn ? (
                                    <input
                                      className="input"
                                      name={c.name}
                                      aria-label={`${c.name} in row ${pk}`}
                                      defaultValue={
                                        row[c.name] === null || row[c.name] === undefined
                                          ? ""
                                          : String(row[c.name])
                                      }
                                    />
                                  ) : (
                                    cellText(row[c.name])
                                  )}
                                </td>
                              ))}
                              <td>
                                {isOpen ? (
                                  <>
                                    <button
                                      type="submit"
                                      className="btn"
                                      disabled={busy === `update:${editingPk}`}
                                    >
                                      {busy === `update:${editingPk}` ? "SAVING…" : "SAVE"}
                                    </button>
                                    <button
                                      type="button"
                                      className="btn btn-ghost"
                                      onClick={() => setEditingPk(null)}
                                    >
                                      Cancel
                                    </button>
                                  </>
                                ) : (
                                  <>
                                    <button
                                      type="button"
                                      className="btn btn-ghost"
                                      aria-label={`Edit row ${pk}`}
                                      disabled={pk === ""}
                                      onClick={() => setEditingPk(pk)}
                                    >
                                      Edit
                                    </button>
                                    <button
                                      type="button"
                                      className="btn btn-ghost"
                                      aria-label={
                                        armed === pk ? `Confirm delete row ${pk}` : `Delete row ${pk}`
                                      }
                                      disabled={pk === "" || busy === `delete:${pk}`}
                                      onClick={() => void handleDelete(pk)}
                                    >
                                      {busy === `delete:${pk}`
                                        ? "DELETING…"
                                        : armed === pk
                                          ? "SURE?"
                                          : "Delete"}
                                    </button>
                                  </>
                                )}
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                </form>
                <p className="status-line">
                  <button
                    type="button"
                    className="btn btn-ghost"
                    disabled={offset === 0 || rowsLoading}
                    onClick={() => setOffset(Math.max(0, offset - LIMIT))}
                  >
                    PREV
                  </button>{" "}
                  {rowsLoading
                    ? "Loading…"
                    : rows.total === 0
                      ? "0 of 0"
                      : `${offset + 1}–${Math.min(offset + LIMIT, rows.total)} of ${plural(rows.total, "row")}`}{" "}
                  <button
                    type="button"
                    className="btn btn-ghost"
                    disabled={offset + LIMIT >= rows.total || rowsLoading}
                    onClick={() => setOffset(offset + LIMIT)}
                  >
                    NEXT
                  </button>
                </p>
              </>
            )}
          </>
        )}
      </Module>

      <Module
        code="DB-03"
        title="Shape and flow"
        span="span-12"
        note="One diagram: live tables and keys above, data in and out below."
      >
        <Legend
          label="Diagram key"
          items={[
            { key: "fk", swatch: <Wire color="var(--sig)" />, text: "physical FK (enforced)" },
            {
              key: "logical",
              swatch: <Wire color="var(--dim)" dashed />,
              text: "source_key value shared, no FK",
            },
            { key: "flow", swatch: <Wire color="var(--amber)" />, text: "data in and out" },
          ]}
        />
        <ErFlowDiagram tables={tables.map((t) => t.name)} schemas={schemas} selected={selected} />
        <p className="status-line">
          Solid lines are enforced foreign keys from the live schema. Dashed stubs share one
          value, <Abbr term="source_key" full="Source key: which site a row came from" />, with no
          database link. Runs do not own postings: a run&apos;s postings are the rows whose{" "}
          <Abbr term="LastSeenAt" full="Last seen at: when a posting was last sighted" /> falls in
          the run window.
        </p>
        <Glossary
          terms={[
            {
              abbr: "PK",
              full: "Primary key",
              hint: "The row identity CRUD uses. Shown on the Tables card and in each schema.",
            },
            {
              abbr: "FK",
              full: "Foreign key",
              hint: "The one enforced link: scrape_runs.profile_id to search_profiles.id.",
            },
            {
              abbr: "CRUD",
              full: "Create, read, update, delete",
              hint: "Insert and edit forms send JSON; deletes arm first, then delete.",
            },
          ]}
        />
      </Module>
    </>
  );
}
