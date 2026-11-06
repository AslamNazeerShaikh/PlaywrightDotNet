import { useEffect, useRef, useState } from "react";

import { CodeTag, Field, Module, plural, RefreshButton } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiClient, ApiProblem, MeResponse, UserResponse } from "../lib/api.ts";
import { canSeeUsers, hasAnyPermission, isTokenFailure } from "../lib/auth.ts";
import { useRenderLog } from "../lib/debug.ts";
import { readText } from "../lib/forms.ts";
import { AuthProblem, fieldErrorsFor } from "./login.tsx";

const ROLES = ["Admin", "Operator", "Viewer"] as const;

/**
 * Users page: the signed-in account (roles, permissions, password change) for every caller, plus
 * the admin surface (list, create with a temporary password, role assign/revoke) when the token
 * carries user visibility. A 401 with a token code signs out through `onUnauthorized`.
 */
export function UsersPage({
  client,
  me,
  onUnauthorized,
}: {
  client: ApiClient;
  me: MeResponse;
  onUnauthorized: () => void;
}) {
  useRenderLog("UsersPage");
  const admin = canSeeUsers(me);
  const canWrite = hasAnyPermission(me, "users.write");
  const [users, setUsers] = useState<UserResponse[] | null>(null);
  const [listProblem, setListProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);
  const [roleNames, setRoleNames] = useState<readonly string[]>(ROLES);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<UserResponse | null>(null);
  const [detailProblem, setDetailProblem] = useState<ApiProblem | null>(null);
  const [creating, setCreating] = useState(false);
  const [createProblem, setCreateProblem] = useState<ApiProblem | null>(null);
  const [changing, setChanging] = useState(false);
  const [changeProblem, setChangeProblem] = useState<ApiProblem | null>(null);
  const [changeSaved, setChangeSaved] = useState(false);
  const [mutating, setMutating] = useState<string | null>(null);
  const [mutateProblem, setMutateProblem] = useState<ApiProblem | null>(null);
  const ticket = useRef(0);

  async function fail(problem: ApiProblem): Promise<boolean> {
    if (isTokenFailure(problem)) {
      onUnauthorized();
      return true;
    }
    return false;
  }

  async function loadUsers() {
    const mine = ++ticket.current;
    setLoading(true);
    try {
      const { result } = await client.listUsers();
      if (mine !== ticket.current) {
        return;
      }
      if (result.ok) {
        setUsers(result.value.users);
        setListProblem(null);
      } else if (!(await fail(result.problem))) {
        setListProblem(result.problem);
      }
      // Role options follow the live catalog: assigning a name no role carries
      // would 404, so offer what the server knows.
      const rolesResult = await client.listRoles();
      if (mine !== ticket.current) {
        return;
      }
      if (rolesResult.result.ok) {
        setRoleNames(rolesResult.result.value.roles.map((r) => r.name));
      }
    } finally {
      if (mine === ticket.current) {
        setLoading(false);
      }
    }
  }

  async function loadDetail(id: string) {
    const { result } = await client.getUser(id);
    if (result.ok) {
      setDetail(result.value);
      setDetailProblem(null);
    } else if (!(await fail(result.problem))) {
      setDetailProblem(result.problem);
    }
  }

  const loadRef = useRef(loadUsers);
  loadRef.current = loadUsers;
  useEffect(() => {
    if (admin) {
      void loadRef.current();
    }
  }, [admin, client]);

  useEffect(() => {
    if (selectedId !== null && admin) {
      void loadDetail(selectedId);
    }
    // loadDetail reads the client prop; re-run only on selection change.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId, admin]);

  async function submitCreate(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    setCreating(true);
    try {
      const { result } = await client.createUser(
        { email: readText(data, "email"), temporaryPassword: readText(data, "temporaryPassword") },
        newIdempotencyKey(),
      );
      if (result.ok) {
        setCreateProblem(null);
        form.reset();
        await loadUsers();
      } else if (!(await fail(result.problem))) {
        setCreateProblem(result.problem);
      }
    } finally {
      setCreating(false);
    }
  }

  async function submitChange(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    setChanging(true);
    try {
      const { result } = await client.changePassword({
        currentPassword: readText(data, "currentPassword"),
        newPassword: readText(data, "newPassword"),
      });
      if (result.ok) {
        setChangeProblem(null);
        setChangeSaved(true);
        form.reset();
      } else if (!(await fail(result.problem))) {
        setChangeProblem(result.problem);
        setChangeSaved(false);
      }
    } finally {
      setChanging(false);
    }
  }

  async function assign(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (selectedId === null) {
      return;
    }
    const role = readText(new FormData(event.currentTarget), "role");
    setMutating(`assign:${role}`);
    try {
      const { result } = await client.assignRole(selectedId, role);
      if (result.ok) {
        setMutateProblem(null);
        await loadDetail(selectedId);
        await loadUsers();
      } else if (!(await fail(result.problem))) {
        setMutateProblem(result.problem);
      }
    } finally {
      setMutating(null);
    }
  }

  async function revoke(role: string) {
    if (selectedId === null) {
      return;
    }
    setMutating(`revoke:${role}`);
    try {
      const { result } = await client.revokeRole(selectedId, role);
      if (result.ok) {
        setMutateProblem(null);
        await loadDetail(selectedId);
        await loadUsers();
      } else if (!(await fail(result.problem))) {
        setMutateProblem(result.problem);
      }
    } finally {
      setMutating(null);
    }
  }

  const createErrors = createProblem?.errors;
  const changeErrors = changeProblem?.errors;

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // USERS" />
        </h1>
        <p>
          The signed-in account first, then every account when the token carries user visibility.
        </p>
      </div>

      <Module
        code="USR-00"
        title="My account"
        span="span-4"
        note="GET /api/v1/auth/me, from the bearer token."
      >
        <div className="tag-row" aria-label="My roles and permissions">
          <CodeTag code={me.email} />
          {me.roles.map((r) => (
            <CodeTag key={r} code={r.toUpperCase()} tone="ok" />
          ))}
          {me.permissions.map((p) => (
            <CodeTag key={p} code={p} />
          ))}
        </div>
        <form onSubmit={submitChange} aria-label="Change my password" noValidate>
          <Field
            label="CURRENT PASSWORD"
            name="currentPassword"
            errors={fieldErrorsFor(changeErrors, "currentPassword")}
          >
            <input
              className="input"
              type="password"
              name="currentPassword"
              aria-label="Current password"
              autoComplete="current-password"
            />
          </Field>
          <Field
            label="NEW PASSWORD"
            name="newPassword"
            errors={fieldErrorsFor(changeErrors, "newPassword")}
          >
            <input
              className="input"
              type="password"
              name="newPassword"
              aria-label="New password"
              autoComplete="new-password"
            />
          </Field>
          <button type="submit" className="btn btn-sm" disabled={changing}>
            {changing ? "SAVING…" : "CHANGE PASSWORD"}
          </button>
        </form>
        {changeSaved && changeProblem === null && (
          <p className="status-line" role="status">
            <CodeTag code="SAVED" tone="ok" /> Password changed.
          </p>
        )}
        {changeProblem !== null && <AuthProblem problem={changeProblem} />}
      </Module>

      {admin && (
        <Module
          code="USR-01"
          title="Users"
          span="span-8"
          note={`${users === null ? "…" : plural(users.length, "user")} · GET /api/v1/users`}
        >
          <div className="toolbar">
            <RefreshButton loading={loading} onRefresh={() => void loadUsers()} />
          </div>
          {listProblem !== null && <AuthProblem problem={listProblem} />}
          {users !== null && users.length === 0 && (
            <p className="status-line">No users yet. Create the first below.</p>
          )}
          {users !== null && users.length > 0 && (
            <div className="tbl-wrap">
              <table className="data" aria-label="Users">
                <thead>
                  <tr>
                    <th>Email</th>
                    <th>Roles</th>
                    <th>First login</th>
                  </tr>
                </thead>
                <tbody>
                  {users.map((u) => (
                    <tr key={u.id} aria-current={u.id === selectedId ? "true" : undefined}>
                      <td>
                        <button
                          type="button"
                          className="row-btn"
                          onClick={() => setSelectedId(u.id)}
                        >
                          {u.email}
                        </button>
                      </td>
                      <td>{u.roles.join(", ") || "—"}</td>
                      <td>{u.mustChangePassword ? "PENDING" : "done"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Module>
      )}

      {admin && canWrite && (
        <Module
          code="USR-02"
          title="New user"
          span="span-4"
          note="POST /api/v1/users with an idempotency key. The account finishes at setup-password."
        >
          <form onSubmit={submitCreate} aria-label="New user" noValidate>
            <Field label="EMAIL" name="email" errors={fieldErrorsFor(createErrors, "email")}>
              <input className="input" name="email" aria-label="Email" autoComplete="off" />
            </Field>
            <Field
              label="TEMPORARY PASSWORD"
              name="temporaryPassword"
              errors={fieldErrorsFor(createErrors, "temporaryPassword")}
            >
              <input
                className="input"
                type="password"
                name="temporaryPassword"
                aria-label="Temporary password"
                autoComplete="new-password"
              />
            </Field>
            <button type="submit" className="btn" disabled={creating}>
              {creating ? "CREATING…" : "CREATE USER"}
            </button>
          </form>
          {createProblem !== null && <AuthProblem problem={createProblem} />}
        </Module>
      )}

      {admin && selectedId !== null && (
        <Module
          code="USR-03"
          title="User detail"
          span="span-8"
          note="GET /api/v1/users/{id} · assign and revoke below."
        >
          {detailProblem !== null && <AuthProblem problem={detailProblem} />}
          {detail !== null && (
            <>
              <div className="tag-row" aria-label={`${detail.email} roles`}>
                <CodeTag code={detail.email} />
                {detail.roles.map((r) => (
                  <CodeTag key={r} code={r.toUpperCase()} tone="ok" />
                ))}
                {detail.mustChangePassword && <CodeTag code="FIRST LOGIN PENDING" tone="warn" />}
              </div>
              <div className="tbl-wrap">
                <table className="data" aria-label={`${detail.email} roles`}>
                  <thead>
                    <tr>
                      <th>Role</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.roles.map((r) => (
                      <tr key={r}>
                        <td>{r}</td>
                        <td className="actions-cell">
                          <button
                            type="button"
                            className="btn btn-sm btn-ghost"
                            disabled={!canWrite || mutating === `revoke:${r}`}
                            onClick={() => void revoke(r)}
                          >
                            {mutating === `revoke:${r}` ? "REVOKING…" : "Revoke"}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {canWrite && (
                <form
                  onSubmit={assign}
                  aria-label={`Assign role to ${detail.email}`}
                  noValidate
                  style={{ marginTop: "var(--gap)" }}
                >
                  <div className="toolbar">
                    <select
                      className="input"
                      name="role"
                      aria-label="Role"
                      defaultValue={roleNames[0] ?? "Viewer"}
                    >
                      {roleNames.map((r) => (
                        <option key={r} value={r}>
                          {r}
                        </option>
                      ))}
                    </select>
                    <button
                      type="submit"
                      className="btn btn-sm"
                      disabled={mutating !== null && mutating.startsWith("assign:")}
                    >
                      ASSIGN ROLE
                    </button>
                  </div>
                </form>
              )}
              {mutateProblem !== null && <AuthProblem problem={mutateProblem} />}
            </>
          )}
        </Module>
      )}

      {!admin && (
        <Module
          code="USR-01"
          title="Users"
          span="span-8"
          note="Admin surface: needs users.read or users.write."
        >
          <p className="alert" role="alert">
            <CodeTag code="ACCESS_DENIED" tone="bad" /> User administration needs the users.read or
            users.write permission.
          </p>
        </Module>
      )}
    </>
  );
}

/** One idempotency key per submit, so a double click never creates two users. */
function newIdempotencyKey(): string {
  try {
    return crypto.randomUUID();
  } catch {
    return `${Date.now()}-${Math.floor(Math.random() * 1_000_000)}`;
  }
}
