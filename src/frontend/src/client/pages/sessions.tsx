import { CodeTag, codeTone, constantCase, Field, Module } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiProblem, CreateSessionInput, SessionPayload } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import { SOURCES } from "./profiles.tsx";

/**
 * Sessions page: create browser profiles (one persistent identity per source) and watch their
 * session health. Logging in and probing need the Playwright session manager, which is not
 * implemented yet; the page says so instead of offering buttons that cannot work.
 */
export const ENGINES = ["Chromium", "Firefox", "WebKit"] as const;
export const CHANNELS = ["Bundled", "Chrome", "Edge"] as const;

export function SessionsPage({
  sessions,
  listProblem,
  loading,
  creating,
  createProblem,
  onCreate,
  onRefresh,
}: {
  sessions: SessionPayload[] | null;
  listProblem: ApiProblem | null;
  loading: boolean;
  creating: boolean;
  createProblem: ApiProblem | null;
  onCreate: (input: CreateSessionInput) => Promise<boolean>;
  onRefresh: () => void;
}) {
  useRenderLog("SessionsPage");
  const fieldErrors = createProblem?.errors ?? {};

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const text = (key: string) => String(data.get(key) ?? "").trim();
    const created = await onCreate({
      name: text("name"),
      sourceKey: text("sourceKey"),
      engine: text("engine"),
      channel: text("channel"),
    });
    if (created) {
      form.reset();
    }
  }

  return (
    <>
      <div className="page-head" id="sessions-div-1">
        <h1 id="sessions-h1-2">
          <Scramble text="OPS-DECK // SESSIONS"  id="sessions-scramble-3"/>
        </h1>
        <p>Persistent browser profiles, one per source, and their session health.</p>
      </div>

      <Module
        code="SES-01"
        title="New browser profile"
        span="span-4"
        note="POST /api/v1/sessions. The name is also the profile's directory."
       id="sessions-module-4">
        <form onSubmit={submit} aria-label="New browser profile" noValidate id="sessions-form-5">
          <Field label="NAME" name="name" errors={fieldErrors} id="sessions-field-6">
            <input
              className="input"
              name="name"
              aria-label="Profile name"
              placeholder="naukri-main"
              autoComplete="off"
              spellCheck={false}
             id="sessions-input-7"/>
          </Field>
          <Field label="SOURCE" name="sourceKey" errors={fieldErrors} id="sessions-field-8">
            <select className="input" name="sourceKey" aria-label="Source" defaultValue="naukri">
              {SOURCES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </Field>
          <div className="field-pair" id="sessions-div-9">
            <Field label="ENGINE" name="engine" errors={fieldErrors} id="sessions-field-10">
              <select className="input" name="engine" aria-label="Engine" defaultValue="Chromium">
                {ENGINES.map((e) => (
                  <option key={e} value={e}>
                    {e}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="CHANNEL" name="channel" errors={fieldErrors} id="sessions-field-11">
              <select className="input" name="channel" aria-label="Channel" defaultValue="Bundled">
                {CHANNELS.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </Field>
          </div>
          <button type="submit" className="btn" disabled={creating}>
            {creating ? "SAVING…" : "CREATE BROWSER PROFILE"}
          </button>
        </form>
        {createProblem !== null && (
          <p className="alert" role="alert" id="sessions-p-12">
            <CodeTag code={createProblem.code} tone={codeTone(createProblem.code)}  id="sessions-codetag-13"/>{" "}
            {createProblem.title}
            {createProblem.detail !== "" ? ` — ${createProblem.detail}` : ""}
          </p>
        )}
        <p className="status-line">
          Login and probe need the Playwright session manager, which is not built yet. New profiles
          stay <CodeTag code="NEW"  id="sessions-codetag-14"/> until a probe verifies them.
        </p>
      </Module>

      <Module
        code="SES-02"
        title="Browser profiles"
        span="span-8"
        note="GET /api/v1/sessions, ordered by name. Launch readiness is evaluated server-side now."
       id="sessions-module-15">
        <button type="button" className="btn btn-ghost" onClick={onRefresh} disabled={loading}>
          {loading ? "LOADING…" : "REFRESH"}
        </button>
        {listProblem !== null && (
          <p className="alert" role="alert" id="sessions-p-16">
            <CodeTag code={listProblem.code} tone={codeTone(listProblem.code)}  id="sessions-codetag-17"/>{" "}
            {listProblem.title}
          </p>
        )}
        {sessions !== null && sessions.length === 0 && (
          <p className="status-line">No browser profiles yet. Create one per source.</p>
        )}
        {sessions !== null && sessions.length > 0 && (
          <div className="tbl-wrap" style={{ marginTop: "10px" }} id="sessions-div-18">
            <table className="data" aria-label="Browser profiles" id="sessions-table-19">
              <thead id="sessions-thead-20">
                <tr id="sessions-tr-21">
                  <th>Name</th>
                  <th>Source</th>
                  <th>Browser</th>
                  <th>State</th>
                  <th>Launch</th>
                  <th>Walls</th>
                  <th>Cooldown until</th>
                  <th>Directory</th>
                </tr>
              </thead>
              <tbody>
                {sessions.map((s) => (
                  <tr key={s.id} id="sessions-tr-22">
                    <td>{s.name}</td>
                    <td id="sessions-td-23">
                      <CodeTag code={s.sourceKey}  id="sessions-codetag-24"/>
                    </td>
                    <td>{`${s.engine} · ${s.channel}`}</td>
                    <td id="sessions-td-25">
                      <CodeTag code={stateLabel(s.state)} tone={stateTone(s.state)}  id="sessions-codetag-26"/>
                    </td>
                    <td id="sessions-td-27">
                      <CodeTag
                        code={s.canLaunch ? "READY" : "BLOCKED"}
                        tone={s.canLaunch ? "ok" : "bad"}
                       id="sessions-codetag-28"/>
                    </td>
                    <td>{s.consecutiveChallenges}</td>
                    <td>{s.cooldownUntil ?? "—"}</td>
                    <td>{s.userDataDirectory}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Module>

      <Module
        code="SES-03"
        title="Session states"
        span="span-12"
        note="The domain's state machine (docs/03-domain-model.md §6.2), rendered verbatim."
       id="sessions-module-29">
        <div className="tbl-wrap" id="sessions-div-30">
          <table className="data" id="sessions-table-31">
            <thead id="sessions-thead-32">
              <tr id="sessions-tr-33">
                <th>State</th>
                <th>Meaning</th>
              </tr>
            </thead>
            <tbody id="sessions-tbody-34">
              <StateRow state="New" text="Never verified, or reset. Needs a login and a probe."  id="sessions-staterow-35"/>
              <StateRow state="Healthy" text="A probe verified the session."  id="sessions-staterow-36"/>
              <StateRow
                state="CoolingDown"
                text="Stopped after a wall; launches are blocked until the cooldown ends."
               id="sessions-staterow-37"/>
              <StateRow state="Expired" text="Cookies expired; a manual re-login is needed."  id="sessions-staterow-38"/>
              <StateRow
                state="ResetRequired"
                text="Three walls in a row; reset or replace the profile."
               id="sessions-staterow-39"/>
            </tbody>
          </table>
        </div>
      </Module>
    </>
  );
}

function StateRow({ state, text }: { state: SessionPayload["state"]; text: string }) {
  return (
    <tr id="sessions-tr-40">
      <td id="sessions-td-41">
        <CodeTag code={stateLabel(state)} tone={stateTone(state)}  id="sessions-codetag-42"/>
      </td>
      <td>{text}</td>
    </tr>
  );
}

/** COOLING_DOWN-style labels so states read like the deck's other codes. */
export function stateLabel(state: SessionPayload["state"]): string {
  return constantCase(state);
}

export function stateTone(state: SessionPayload["state"]): "ok" | "warn" | "bad" | undefined {
  if (state === "Healthy") {
    return "ok";
  }
  if (state === "ResetRequired") {
    return "bad";
  }
  return state === "New" ? undefined : "warn";
}
