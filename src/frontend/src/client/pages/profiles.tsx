import { Abbr } from "../components/legend.tsx";
import { CodeTag, codeTone, Field, JsonBlock, Module, plural } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiProblem, CreateProfileInput, SearchProfilePayload } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";

/**
 * Search-profile management: create form with inline server field errors, the persisted profile
 * table, and a detail panel for the selected row. The form is uncontrolled (no per-keystroke state,
 * happy-dom-safe); the server is the only validator, so every rule the API enforces surfaces here
 * verbatim, keyed by its JSON field name.
 */
export const SOURCES = ["naukri", "indeed", "linkedin"] as const;

export function ProfilesPage({
  profiles,
  listProblem,
  loading,
  creating,
  createProblem,
  selectedId,
  onSelect,
  onCreate,
  onRefresh,
}: {
  profiles: SearchProfilePayload[] | null;
  listProblem: ApiProblem | null;
  loading: boolean;
  creating: boolean;
  createProblem: ApiProblem | null;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onCreate: (input: CreateProfileInput) => Promise<boolean>;
  onRefresh: () => void;
}) {
  useRenderLog("ProfilesPage");
  const selected = profiles?.find((p) => p.id === selectedId) ?? null;
  const fieldErrors = createProblem?.errors ?? {};

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const created = await onCreate(readForm(new FormData(form)));
    if (created) {
      form.reset();
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // SEARCH PROFILES" />
        </h1>
        <p>
          Saved searches, persisted in SQLite. Caps start at the source policy and can only tighten.
        </p>
      </div>

      <Module
        code="PRF-01"
        title="New profile"
        span="span-5"
        note={
          <>
            POST /api/v1/search-profiles through the <Abbr term="BFF" full="Backend for Frontend" />
            . Field errors come from the server.
          </>
        }
      >
        <form onSubmit={submit} aria-label="New search profile" noValidate>
          <Field label="NAME" name="name" errors={fieldErrors}>
            <input className="input" name="name" aria-label="Name" autoComplete="off" />
          </Field>
          <Field label="SOURCE" name="sourceKey" errors={fieldErrors}>
            <select className="input" name="sourceKey" aria-label="Source" defaultValue="naukri">
              {SOURCES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </Field>
          <Field label="KEYWORDS" name="keywords" errors={fieldErrors}>
            <input
              className="input"
              name="keywords"
              aria-label="Keywords"
              placeholder="asp.net core c#"
              autoComplete="off"
            />
          </Field>
          <Field label="LOCATIONS (COMMA-SEPARATED)" name="locations" errors={fieldErrors}>
            <input
              className="input"
              name="locations"
              aria-label="Locations"
              placeholder="Pune, Hyderabad"
              autoComplete="off"
            />
          </Field>
          <div className="field-pair">
            <Field label="EXPERIENCE (YRS)" name="experienceYears" errors={fieldErrors}>
              <input
                className="input"
                name="experienceYears"
                aria-label="Experience years"
                inputMode="numeric"
              />
            </Field>
            <Field label="POSTED WITHIN (DAYS)" name="postedWithinDays" errors={fieldErrors}>
              <input
                className="input"
                name="postedWithinDays"
                aria-label="Posted within days"
                inputMode="numeric"
              />
            </Field>
          </div>
          <label className="check">
            <input type="checkbox" name="remoteOnly" aria-label="Remote only" /> REMOTE ONLY
          </label>
          <button type="submit" className="btn" disabled={creating}>
            {creating ? "SAVING…" : "CREATE PROFILE"}
          </button>
        </form>
        {createProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={createProblem.code} tone={codeTone(createProblem.code)} />{" "}
            {createProblem.title}
            {createProblem.detail !== "" ? ` — ${createProblem.detail}` : ""}
          </p>
        )}
      </Module>

      <Module
        code="PRF-02"
        title="Profiles"
        span="span-7"
        note="GET /api/v1/search-profiles, ordered by name. Select a name for detail."
      >
        <button type="button" className="btn btn-ghost" onClick={onRefresh} disabled={loading}>
          {loading ? "LOADING…" : "REFRESH"}
        </button>
        {listProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={listProblem.code} tone={codeTone(listProblem.code)} />{" "}
            {listProblem.title}
          </p>
        )}
        {profiles !== null && profiles.length === 0 && (
          <p className="status-line">No profiles yet. Create one to get started.</p>
        )}
        {profiles !== null && profiles.length > 0 && (
          <div className="tbl-wrap" style={{ marginTop: "10px" }}>
            <table className="data" aria-label="Search profiles">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Source</th>
                  <th>Keywords</th>
                  <th>Locations</th>
                  <th>Caps</th>
                  <th>State</th>
                </tr>
              </thead>
              <tbody>
                {profiles.map((p) => (
                  <tr key={p.id} aria-selected={p.id === selectedId}>
                    <td>
                      <button type="button" className="row-btn" onClick={() => onSelect(p.id)}>
                        {p.name}
                      </button>
                    </td>
                    <td>
                      <CodeTag code={p.sourceKey} />
                    </td>
                    <td>{p.keywords}</td>
                    <td>{p.remoteOnly ? "remote" : p.locations.join(", ")}</td>
                    <td>{`${p.maxPages}p · ${p.maxJobs}j · ${p.maxDetails}d`}</td>
                    <td>
                      <CodeTag
                        code={p.enabled ? "ENABLED" : "DISABLED"}
                        tone={p.enabled ? "ok" : "warn"}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Module>

      <Module
        code="PRF-03"
        title="Profile detail"
        span="span-7"
        note="Every stored field, plus the exact payload."
      >
        {selected === null ? (
          <p className="status-line">Select a profile name to see its detail.</p>
        ) : (
          <>
            <div className="tbl-wrap">
              <table className="data" aria-label="Profile detail">
                <tbody>
                  <DetailRow label="ID" value={selected.id} />
                  <DetailRow label="Name" value={selected.name} />
                  <DetailRow label="Source" value={selected.sourceKey} />
                  <DetailRow label="Keywords" value={selected.keywords} />
                  <DetailRow
                    label="Locations"
                    value={selected.locations.length > 0 ? selected.locations.join(", ") : "—"}
                  />
                  <DetailRow label="Experience" value={orDash(selected.experienceYears, " yrs")} />
                  <DetailRow
                    label="Posted within"
                    value={orDash(selected.postedWithinDays, " days")}
                  />
                  <DetailRow label="Remote only" value={selected.remoteOnly ? "yes" : "no"} />
                  <DetailRow
                    label="Caps"
                    value={`${plural(selected.maxPages, "page")} · ${plural(selected.maxJobs, "job")} · ${plural(selected.maxDetails, "detail")}`}
                  />
                  <DetailRow
                    label="Delay window"
                    value={`${selected.minDelayMs}–${selected.maxDelayMs} ms`}
                  />
                  <DetailRow label="Created (UTC)" value={selected.createdAt} />
                </tbody>
              </table>
            </div>
            <div style={{ marginTop: "10px" }}>
              <JsonBlock label="Profile payload" value={selected} />
            </div>
          </>
        )}
      </Module>

      <Module
        code="PRF-04"
        title="Code reference"
        span="span-5"
        note="The contract for these endpoints. UI branches on code."
      >
        <div className="tbl-wrap">
          <table className="data">
            <thead>
              <tr>
                <th>Code</th>
                <th>HTTP</th>
                <th>Meaning</th>
              </tr>
            </thead>
            <tbody>
              <CodeRow code="OK" http="200/201" text="Listed, fetched, or created." />
              <CodeRow
                code="VALIDATION_FAILED"
                http="400"
                text="Field errors shown under each input."
              />
              <CodeRow
                code="PROFILE_NAME_TAKEN"
                http="409"
                text="Another profile already uses that name."
              />
              <CodeRow code="PROFILE_NOT_FOUND" http="404" text="No profile with that id." />
              <CodeRow
                code="DOTNET_UNREACHABLE"
                http="502"
                text="BFF alive, API down. Start the data plane."
              />
            </tbody>
          </table>
        </div>
      </Module>
    </>
  );
}

/** Reads the uncontrolled form into the API body; blank numbers become null. */
export function readForm(data: FormData): CreateProfileInput {
  const text = (key: string) => String(data.get(key) ?? "").trim();
  const number = (key: string) => {
    const raw = text(key);
    return raw === "" ? null : Number(raw);
  };
  return {
    name: text("name"),
    sourceKey: text("sourceKey"),
    keywords: text("keywords"),
    locations: text("locations")
      .split(",")
      .map((l) => l.trim())
      .filter((l) => l !== ""),
    experienceYears: number("experienceYears"),
    postedWithinDays: number("postedWithinDays"),
    remoteOnly: data.get("remoteOnly") !== null,
  };
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <tr>
      <th>{label}</th>
      <td>{value}</td>
    </tr>
  );
}

function CodeRow({ code, http, text }: { code: string; http: string; text: string }) {
  return (
    <tr>
      <td>
        <CodeTag code={code} tone={codeTone(code)} />
      </td>
      <td>{http}</td>
      <td>{text}</td>
    </tr>
  );
}

function orDash(value: number | null, unit: string): string {
  return value === null ? "—" : `${value}${unit}`;
}
