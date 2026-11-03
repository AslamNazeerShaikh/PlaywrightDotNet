import { useEffect, useState } from "react";

import type {
  ApiClient,
  DbColumnInfo,
  DbForeignKeyInfo,
  DbRowsResult,
  DbTableInfo,
  DbTableSchema,
} from "../lib/api.ts";

export function DatabasePage({ client }: { client: ApiClient }) {
  const [tables, setTables] = useState<DbTableInfo[]>([]);
  const [problem, setProblem] = useState<string | null>(null);
  const [selected, setSelected] = useState<string | null>(null);
  const [schema, setSchema] = useState<DbTableSchema | null>(null);
  const [rows, setRows] = useState<DbRowsResult | null>(null);
  const [offset, setOffset] = useState(0);
  const [editing, setEditing] = useState<Record<string, string> | null>(null);
  const [creating, setCreating] = useState(false);
  const [newRow, setNewRow] = useState<Record<string, string>>({});
  const [msg, setMsg] = useState<string | null>(null);
  const LIMIT = 25;

  async function loadTables() {
    const r = await client.listDbTables();
    if (r.result.ok) {
      setTables(r.result.value.items);
      if (!selected && r.result.value.items.length) {
        setSelected(r.result.value.items[0]!.name);
      }
    } else {
      setProblem(r.result.problem.detail);
    }
  }

  async function loadSchemaAndRows(table: string) {
    const s = await client.getDbSchema(table);
    if (s.result.ok) setSchema(s.result.value);
    const rr = await client.listDbRows(table, offset, LIMIT);
    if (rr.result.ok) setRows(rr.result.value);
  }

  useEffect(() => {
    void loadTables();
  }, []);

  useEffect(() => {
    if (selected) void loadSchemaAndRows(selected);
  }, [selected, offset]);

  function coerce(col: DbColumnInfo, raw: string): unknown {
    if (raw === "" && !col.notNull) return null;
    const t = col.type.toUpperCase();
    if (t.includes("BOOL")) {
      if (raw.toLowerCase() === "true") return true;
      if (raw.toLowerCase() === "false") return false;
    }
    if (
      t.includes("INT") ||
      t.includes("REAL") ||
      t.includes("NUM") ||
      t.includes("FLOA") ||
      t.includes("DOUB")
    ) {
      const n = Number(raw);
      if (!Number.isNaN(n) && raw.trim() !== "") return n;
    }
    return raw;
  }

  async function handleInsert() {
    if (!selected || !schema) return;
    const values: Record<string, unknown> = {};
    for (const c of schema.columns) {
      const raw = newRow[c.name] ?? "";
      if (raw === "" && !c.notNull) {
        // skip nulls to let DB default apply
      } else if (raw !== "") {
        values[c.name] = coerce(c, raw);
      }
    }
    const r = await client.insertDbRow(selected, values);
    if (r.result.ok) {
      setMsg("Inserted.");
      setCreating(false);
      setNewRow({});
      await loadSchemaAndRows(selected);
      await loadTables();
    } else {
      setMsg(r.result.problem.detail);
    }
  }

  async function handleUpdate() {
    if (!selected || !schema || !editing) return;
    const pk = schema.primaryKeyColumn;
    const pkVal = editing[pk] ?? "";
    const values: Record<string, unknown> = {};
    for (const c of schema.columns) {
      if (c.name === pk) continue;
      const raw = editing[c.name] ?? "";
      values[c.name] = coerce(c, raw);
    }
    const r = await client.updateDbRow(selected, pkVal, values);
    if (r.result.ok) {
      setMsg("Updated.");
      setEditing(null);
      await loadSchemaAndRows(selected);
    } else {
      setMsg(r.result.problem.detail);
    }
  }

  async function handleDelete(pkVal: string) {
    if (!selected) return;
    if (!confirm(`Delete row ${pkVal}?`)) return;
    const r = await client.deleteDbRow(selected, pkVal);
    if (r.result.ok) {
      setMsg("Deleted.");
      await loadSchemaAndRows(selected);
      await loadTables();
    } else {
      setMsg(r.result.problem.detail);
    }
  }

  return (
    <div className="page">
      <h1>Database explorer</h1>
      <p>Tables, schema, rows, and CRUD against the SQLite store.</p>
      {problem && <p style={{ color: "red" }}>{problem}</p>}
      {msg && <p>{msg}</p>}
      <div style={{ display: "flex", gap: 16 }}>
        <aside style={{ width: 220 }}>
          <h3>Tables</h3>
          <ul>
            {tables.map((t) => (
              <li key={t.name}>
                <button
                  onClick={() => {
                    setOffset(0);
                    setSelected(t.name);
                  }}
                >
                  {t.name} ({t.rowCount})
                </button>
              </li>
            ))}
          </ul>
        </aside>
        <main style={{ flex: 1 }}>
          {schema && (
            <>
              <h2>{schema.name}</h2>
              <h3>Schema</h3>
              <table className="data">
                <thead>
                  <tr>
                    <th>Column</th>
                    <th>Type</th>
                    <th>Not null</th>
                    <th>PK</th>
                    <th>Default</th>
                  </tr>
                </thead>
                <tbody>
                  {schema.columns.map((c) => (
                    <tr key={c.name}>
                      <td>{c.name}</td>
                      <td>{c.type}</td>
                      <td>{c.notNull ? "yes" : ""}</td>
                      <td>{c.primaryKey ? "yes" : ""}</td>
                      <td>{c.defaultValue ?? ""}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {schema.foreignKeys.length > 0 && (
                <>
                  <h3>Foreign keys</h3>
                  <ul>
                    {schema.foreignKeys.map((fk, i) => (
                      <li key={i}>
                        {fk.column} -&gt; {fk.referencedTable}.{fk.referencedColumn}
                      </li>
                    ))}
                  </ul>
                </>
              )}
              <h3>Rows</h3>
              <button onClick={() => setCreating(true)}>New row</button>
              {creating && schema && (
                <div>
                  {schema.columns.map((c) => (
                    <input
                      key={c.name}
                      placeholder={c.name}
                      value={newRow[c.name] ?? ""}
                      onChange={(e) => setNewRow({ ...newRow, [c.name]: e.target.value })}
                    />
                  ))}
                  <button onClick={handleInsert}>Save</button>
                  <button onClick={() => setCreating(false)}>Cancel</button>
                </div>
              )}
              {rows && (
                <>
                  <table className="data">
                    <thead>
                      <tr>
                        {schema.columns.map((c) => (
                          <th key={c.name}>{c.name}</th>
                        ))}
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows.rows.map((r, i) => (
                        <tr key={i}>
                          {schema.columns.map((c) => (
                            <td key={c.name}>
                              {editing &&
                              editing[schema.primaryKeyColumn] ===
                                String(r[schema.primaryKeyColumn]) ? (
                                <input
                                  value={editing[c.name] ?? ""}
                                  onChange={(e) =>
                                    setEditing({ ...editing, [c.name]: e.target.value })
                                  }
                                />
                              ) : (
                                String(r[c.name] ?? "")
                              )}
                            </td>
                          ))}
                          <td>
                            {editing &&
                            editing[schema.primaryKeyColumn] ===
                              String(r[schema.primaryKeyColumn]) ? (
                              <>
                                <button onClick={handleUpdate}>Save</button>
                                <button onClick={() => setEditing(null)}>Cancel</button>
                              </>
                            ) : (
                              <>
                                <button
                                  onClick={() =>
                                    setEditing(
                                      Object.fromEntries(
                                        schema.columns.map((c) => [
                                          c.name,
                                          String(r[c.name] ?? ""),
                                        ]),
                                      ),
                                    )
                                  }
                                >
                                  Edit
                                </button>
                                <button
                                  onClick={() => handleDelete(String(r[schema.primaryKeyColumn]))}
                                >
                                  Delete
                                </button>
                              </>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  <div>
                    <button
                      disabled={offset === 0}
                      onClick={() => setOffset(Math.max(0, offset - LIMIT))}
                    >
                      Prev
                    </button>
                    <span>
                      {" "}
                      {rows.total === 0
                        ? "0 of 0"
                        : `${offset + 1}-${Math.min(offset + LIMIT, rows.total)} of ${rows.total}`}{" "}
                    </span>
                    <button
                      disabled={offset + LIMIT >= rows.total}
                      onClick={() => setOffset(offset + LIMIT)}
                    >
                      Next
                    </button>
                  </div>
                </>
              )}
              <h3>ER and data flow</h3>
              <svg viewBox="0 0 760 320" className="diagram">
                {(() => {
                  const tables = [
                    "search_profiles",
                    "scrape_runs",
                    "scrape_run_progress",
                    "job_postings",
                    "browser_profiles",
                    "source_quotas",
                    "source_circuit_breakers",
                  ];
                  const pos: Record<string, [number, number]> = {
                    search_profiles: [40, 40],
                    scrape_runs: [260, 40],
                    scrape_run_progress: [480, 40],
                    job_postings: [260, 160],
                    browser_profiles: [40, 160],
                    source_quotas: [480, 160],
                    source_circuit_breakers: [40, 280],
                  };
                  return (
                    <>
                      {tables.map((t) => {
                        const [x, y] = pos[t]!;
                        return (
                          <g key={t}>
                            <rect
                              x={x}
                              y={y}
                              width="180"
                              height="28"
                              rx="4"
                              fill="var(--canvas)"
                              stroke="var(--line-hi)"
                            />
                            <text
                              x={x + 90}
                              y={y + 18}
                              textAnchor="middle"
                              fontSize="11"
                              fill="var(--ink)"
                            >
                              {t}
                            </text>
                          </g>
                        );
                      })}
                      <line
                        x1={220}
                        y1={54}
                        x2={260}
                        y2={54}
                        stroke="var(--sig)"
                        strokeWidth="1.5"
                      />
                      <line
                        x1={440}
                        y1={54}
                        x2={480}
                        y2={54}
                        stroke="var(--sig)"
                        strokeWidth="1.5"
                      />
                      <line
                        x1={350}
                        y1={68}
                        x2={350}
                        y2={160}
                        stroke="var(--dim)"
                        strokeWidth="1"
                        strokeDasharray="4 4"
                      />
                      <line
                        x1={130}
                        y1={68}
                        x2={130}
                        y2={160}
                        stroke="var(--dim)"
                        strokeWidth="1"
                        strokeDasharray="4 4"
                      />
                      <line
                        x1={570}
                        y1={68}
                        x2={570}
                        y2={160}
                        stroke="var(--dim)"
                        strokeWidth="1"
                        strokeDasharray="4 4"
                      />
                      <text x={230} y={48} fontSize="9" fill="var(--dim)">
                        FK profile_id
                      </text>
                      <text x={450} y={48} fontSize="9" fill="var(--dim)">
                        run id PK
                      </text>
                      <text x={356} y={120} fontSize="9" fill="var(--dim)">
                        source_key
                      </text>
                      <text x={136} y={120} fontSize="9" fill="var(--dim)">
                        source_key
                      </text>
                      <text x={576} y={120} fontSize="9" fill="var(--dim)">
                        source_key
                      </text>
                    </>
                  );
                })()}
              </svg>
              <p className="status-line">
                Solid lines are the one physical FK (scrape_runs.profile_id to search_profiles).
                Dashed lines are logical links by source_key. Data flows in through scrape_runs and
                job_postings; out through run postings windows and exports.
              </p>
            </>
          )}
        </main>
      </div>
    </div>
  );
}
