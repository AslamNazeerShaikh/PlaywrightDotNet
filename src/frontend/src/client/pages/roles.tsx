import { useEffect, useRef, useState } from "react";

import { CodeTag, Field, Module, plural, RefreshButton } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import {
  type ApiClient,
  type ApiProblem,
  PERMISSION_CATALOG,
  type RoleResponse,
} from "../lib/api.ts";
import { hasAnyPermission, isTokenFailure } from "../lib/auth.ts";
import { useRenderLog } from "../lib/debug.ts";
import { readText } from "../lib/forms.ts";
import { AuthProblem, fieldErrorsFor } from "./login.tsx";

/**
 * Roles page: list, create, and per-role permission grants across the 11 catalog keys. Grant and
 * revoke are one checkbox each; the toggle disables while its own request flies so a double click
 * never fires two grants. A 401 with a token code signs out through `onUnauthorized`.
 */
export function RolesPage({
  client,
  canWrite,
  onUnauthorized,
}: {
  client: ApiClient;
  canWrite: boolean;
  onUnauthorized: (problem: ApiProblem) => void;
}) {
  useRenderLog("RolesPage");
  const [roles, setRoles] = useState<RoleResponse[] | null>(null);
  const [listProblem, setListProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [createName, setCreateName] = useState<string>("");
  const [createProblem, setCreateProblem] = useState<ApiProblem | null>(null);
  const [toggling, setToggling] = useState<string | null>(null);
  const [toggleProblem, setToggleProblem] = useState<ApiProblem | null>(null);
  const ticket = useRef(0);

  async function fail(problem: ApiProblem): Promise<boolean> {
    if (isTokenFailure(problem)) {
      onUnauthorized(problem);
      return true;
    }
    return false;
  }

  async function loadRoles() {
    const mine = ++ticket.current;
    setLoading(true);
    try {
      const { result } = await client.listRoles();
      if (mine !== ticket.current) {
        return;
      }
      if (result.ok) {
        setRoles(result.value.roles);
        setListProblem(null);
        // Keep the pinned role across reloads; re-pin to the first only when it vanished.
        setSelectedName((current) =>
          current !== null && result.value.roles.some((r) => r.name === current)
            ? current
            : (result.value.roles[0]?.name ?? null),
        );
      } else if (!(await fail(result.problem))) {
        setListProblem(result.problem);
      }
    } finally {
      if (mine === ticket.current) {
        setLoading(false);
      }
    }
  }

  const loadRef = useRef(loadRoles);
  loadRef.current = loadRoles;
  useEffect(() => {
    void loadRef.current();
    // loadRoles reads the client prop; run once per visit.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [client]);

  async function submitCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    setCreating(true);
    try {
      const name = readText(new FormData(form), "name");
      if (!name) {
        return;
      }
      const { result } = await client.createRole({ name }, newIdempotencyKey());
      if (result.ok) {
        setCreateProblem(null);
        form.reset();
        setSelectedName(name);
        await loadRoles();
      } else if (!(await fail(result.problem))) {
        setCreateProblem(result.problem);
      }
    } finally {
      setCreating(false);
    }
  }

  async function toggle(role: string, key: string, granted: boolean) {
    setToggling(`${role}:${key}`);
    try {
      const { result } = granted
        ? await client.revokePermission(role, key)
        : await client.grantPermission(role, key);
      if (result.ok) {
        setToggleProblem(null);
        await loadRoles();
      } else if (!(await fail(result.problem))) {
        setToggleProblem(result.problem);
      }
    } finally {
      setToggling(null);
    }
  }

  const selected = roles?.find((r) => r.name === selectedName) ?? null;
  const existingNames = new Set((roles ?? []).map((r) => r.name));
  const creatableNames = (["Admin", "Operator", "Viewer"] as const).filter(
    (name) => !existingNames.has(name),
  );

  useEffect(() => {
    setCreateName((current) =>
      creatableNames.includes(current as (typeof creatableNames)[number])
        ? current
        : (creatableNames[0] ?? ""),
    );
  }, [creatableNames.join(",")]);

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // ROLES" />
        </h1>
        <p>Role names and their permission grants, exactly as the token will carry them.</p>
      </div>

      <Module
        code="ROL-01"
        title="Roles"
        span="span-4"
        note={`${roles === null ? "…" : plural(roles.length, "role")} · GET /api/v1/roles`}
      >
        <div className="toolbar">
          <RefreshButton loading={loading} onRefresh={() => void loadRoles()} />
        </div>
        {listProblem !== null && <AuthProblem problem={listProblem} />}
        {roles !== null && (
          <div className="tbl-wrap">
            <table className="data" aria-label="Roles">
              <thead>
                <tr>
                  <th>Role</th>
                  <th>Grants</th>
                </tr>
              </thead>
              <tbody>
                {roles.map((r) => (
                  <tr key={r.name} aria-current={r.name === selectedName ? "true" : undefined}>
                    <td>
                      <button
                        type="button"
                        className="row-btn"
                        onClick={() => setSelectedName(r.name)}
                      >
                        {r.name}
                      </button>
                    </td>
                    <td>{r.permissions.length}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Module>

      {canWrite && (
        <Module
          code="ROL-02"
          title="New role"
          span="span-4"
          note="POST /api/v1/roles with an idempotency key. Grants start empty."
        >
          <form onSubmit={submitCreate} aria-label="New role" noValidate>
            <Field label="NAME" name="name" errors={fieldErrorsFor(createProblem?.errors, "name")}>
              <select
                className="input"
                name="name"
                aria-label="Name"
                value={createName}
                onChange={(event) => setCreateName(event.target.value)}
                disabled={creatableNames.length === 0}
              >
                {creatableNames.length === 0 && <option value="">All roles already exist</option>}
                {creatableNames.map((name) => (
                  <option key={name} value={name}>
                    {name}
                  </option>
                ))}
              </select>
            </Field>
            <button
              type="submit"
              className="btn"
              disabled={creating || creatableNames.length === 0}
            >
              {creating ? "CREATING…" : "CREATE ROLE"}
            </button>
          </form>
          {createProblem !== null && <AuthProblem problem={createProblem} />}
        </Module>
      )}

      <Module
        code="ROL-03"
        title={selected === null ? "Permissions" : `${selected.name} permissions`}
        span="span-8"
        note="11 catalog keys · POST/DELETE /api/v1/roles/{name}/permissions/{key}."
      >
        {selected === null && <p className="status-line">Pick a role.</p>}
        {selected !== null && (
          <>
            <div className="tbl-wrap">
              <table className="data" aria-label={`${selected.name} permissions`}>
                <thead>
                  <tr>
                    <th>Key</th>
                    <th>Granted</th>
                  </tr>
                </thead>
                <tbody>
                  {PERMISSION_CATALOG.map((key) => {
                    const granted = selected.permissions.includes(key);
                    const busy = toggling === `${selected.name}:${key}`;
                    return (
                      <tr key={key}>
                        <td>
                          <CodeTag code={key} tone={granted ? "ok" : undefined} />
                        </td>
                        <td>
                          <label className="check">
                            <input
                              type="checkbox"
                              checked={granted}
                              disabled={!canWrite || busy}
                              aria-label={`${granted ? "Revoke" : "Grant"} ${key} on ${selected.name}`}
                              onChange={() => void toggle(selected.name, key, granted)}
                            />
                            {busy ? "…" : granted ? "yes" : "no"}
                          </label>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            {toggleProblem !== null && <AuthProblem problem={toggleProblem} />}
            {!canWrite && (
              <p className="status-line">
                <CodeTag code="ACCESS_DENIED" tone="bad" /> Grants need roles.write.
              </p>
            )}
          </>
        )}
      </Module>
    </>
  );
}

/** One idempotency key per submit, so a double click never creates two roles. */
function newIdempotencyKey(): string {
  try {
    return crypto.randomUUID();
  } catch {
    return `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
  }
}

/** Re-exported for tests: write access to role grants needs roles.write. */
export function canWriteRoles(permissions: string[]): boolean {
  return hasAnyPermission({ email: "", roles: [], permissions }, "roles.write");
}
