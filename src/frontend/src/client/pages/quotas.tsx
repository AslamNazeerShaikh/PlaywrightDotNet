import { useEffect, useRef, useState } from "react";

import { CodeTag, Field, Module, plural, RefreshButton } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiClient, ApiProblem, QuotaResponse } from "../lib/api.ts";
import { isTokenFailure } from "../lib/auth.ts";
import { useRenderLog } from "../lib/debug.ts";
import { readNumber, readText } from "../lib/forms.ts";
import { AuthProblem, fieldErrorsFor } from "./login.tsx";

/**
 * Quotas page: every usage quota definition with its per-user limit and fixed window, plus an
 * editor per scope. A 401 with a token code signs out through `onUnauthorized`.
 */
export function QuotasPage({
  client,
  canWrite,
  onUnauthorized,
}: {
  client: ApiClient;
  canWrite: boolean;
  onUnauthorized: () => void;
}) {
  useRenderLog("QuotasPage");
  const [quotas, setQuotas] = useState<QuotaResponse[] | null>(null);
  const [listProblem, setListProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState<string | null>(null);
  const [saveProblems, setSaveProblems] = useState<Record<string, ApiProblem>>({});
  const [saved, setSaved] = useState<string | null>(null);
  const ticket = useRef(0);

  async function fail(problem: ApiProblem): Promise<boolean> {
    if (isTokenFailure(problem)) {
      onUnauthorized();
      return true;
    }
    return false;
  }

  async function loadQuotas() {
    const mine = ++ticket.current;
    setLoading(true);
    try {
      const { result } = await client.listQuotas();
      if (mine !== ticket.current) {
        return;
      }
      if (result.ok) {
        setQuotas(result.value.quotas);
        setListProblem(null);
      } else if (!(await fail(result.problem))) {
        setListProblem(result.problem);
      }
    } finally {
      if (mine === ticket.current) {
        setLoading(false);
      }
    }
  }

  const loadRef = useRef(loadQuotas);
  loadRef.current = loadQuotas;
  useEffect(() => {
    void loadRef.current();
    // loadQuotas reads the client prop; run once per visit.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [client]);

  async function submit(event: React.FormEvent<HTMLFormElement>, scope: string) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const limit = readNumber(data, "limit");
    const window = readText(data, "window");
    if (limit === null) {
      setSaveProblems((s) => ({
        ...s,
        [scope]: {
          status: 400,
          title: "Limit is required.",
          detail: "Enter a positive whole number.",
          code: "VALIDATION_FAILED",
          errors: { limit: ["Enter a positive whole number."] },
        },
      }));
      return;
    }
    setSaving(scope);
    try {
      const { result } = await client.updateQuota(scope, { limit, window });
      if (result.ok) {
        setSaveProblems((s) => {
          const next = { ...s };
          delete next[scope];
          return next;
        });
        setSaved(scope);
        await loadQuotas();
      } else if (!(await fail(result.problem))) {
        setSaved((current) => (current === scope ? null : current));
        setSaveProblems((s) => ({ ...s, [scope]: result.problem }));
      }
    } finally {
      setSaving(null);
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // QUOTAS" />
        </h1>
        <p>Per-user request budgets per scope. The window is a fixed span like 00:01:00.</p>
      </div>

      <Module
        code="QUO-01"
        title="Quotas"
        span="span-12"
        note={`${quotas === null ? "…" : plural(quotas.length, "scope")} · GET /api/v1/quotas`}
      >
        <div className="toolbar">
          <RefreshButton loading={loading} onRefresh={() => void loadQuotas()} />
        </div>
        {listProblem !== null && <AuthProblem problem={listProblem} />}
        {quotas !== null && quotas.length === 0 && (
          <p className="status-line">No quota scopes defined.</p>
        )}
        {quotas !== null && quotas.length > 0 && (
          <div className="tbl-wrap">
            <table className="data" aria-label="Usage quotas">
              <thead>
                <tr>
                  <th>Scope</th>
                  <th>Limit</th>
                  <th>Window</th>
                  {canWrite && <th>Edit</th>}
                </tr>
              </thead>
              <tbody>
                {quotas.map((q) => (
                  <QuotaRow
                    key={q.scope}
                    quota={q}
                    canWrite={canWrite}
                    saving={saving === q.scope}
                    saved={saved === q.scope}
                    problem={saveProblems[q.scope] ?? null}
                    onSubmit={(e) => void submit(e, q.scope)}
                  />
                ))}
              </tbody>
            </table>
          </div>
        )}
        {!canWrite && (
          <p className="status-line">
            <CodeTag code="ACCESS_DENIED" tone="bad" /> Editing quotas needs roles.write.
          </p>
        )}
      </Module>
    </>
  );
}

function QuotaRow({
  quota,
  canWrite,
  saving,
  saved,
  problem,
  onSubmit,
}: {
  quota: QuotaResponse;
  canWrite: boolean;
  saving: boolean;
  saved: boolean;
  problem: ApiProblem | null;
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <tr>
      <td>
        <CodeTag code={quota.scope} />
      </td>
      <td>{quota.limit}</td>
      <td>{quota.window}</td>
      {canWrite && (
        <td>
          <form
            onSubmit={onSubmit}
            aria-label={`Edit ${quota.scope} quota`}
            noValidate
            style={{ display: "flex", gap: "8px", alignItems: "end", flexWrap: "wrap" }}
          >
            <Field label="LIMIT" name="limit" errors={fieldErrorsFor(problem?.errors, "limit")}>
              <input
                className="input"
                name="limit"
                aria-label={`${quota.scope} limit`}
                inputMode="numeric"
                defaultValue={String(quota.limit)}
                style={{ width: "7em" }}
              />
            </Field>
            <Field label="WINDOW" name="window" errors={fieldErrorsFor(problem?.errors, "window")}>
              <input
                className="input"
                name="window"
                aria-label={`${quota.scope} window`}
                defaultValue={quota.window}
                style={{ width: "9em" }}
              />
            </Field>
            <button type="submit" className="btn btn-sm" disabled={saving}>
              {saving ? "SAVING…" : "SAVE"}
            </button>
            {saved && problem === null && !saving && (
              <span role="status">
                <CodeTag code="SAVED" tone="ok" />
              </span>
            )}
          </form>
          {problem !== null && <AuthProblem problem={problem} />}
        </td>
      )}
    </tr>
  );
}
