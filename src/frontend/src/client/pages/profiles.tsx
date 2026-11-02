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
      <div className="page-head" id="profiles-div-1">
        <h1 id="profiles-h1-2">
          <Scramble text="OPS-DECK // SEARCH PROFILES"  id="profiles-scramble-3"/>
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
            POST /api/v1/search-profiles through the <Abbr term="BFF" full="Backend for Frontend"  id="profiles-abbr-4"/>
            . Field errors come from the server.
          </>
        }
      >
        <form onSubmit={submit} aria-label="New search profile" noValidate id="profiles-form-5">
          <Field label="NAME" name="name" errors={fieldErrors} id="profiles-field-6">
            <input className="input" name="name" aria-label="Name" autoComplete="off"  id="profiles-input-7"/>
          </Field>
          <Field label="SOURCE" name="sourceKey" errors={fieldErrors} id="profiles-field-8">
            <select className="input" name="sourceKey" aria-label="Source" defaultValue="naukri">
              {SOURCES.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>
          </Field>
          <Field label="KEYWORDS" name="keywords" errors={fieldErrors} id="profiles-field-9">
            <input
              className="input"
              name="keywords"
              aria-label="Keywords"
              placeholder="asp.net core c#"
              autoComplete="off"
             id="profiles-input-10"/>
          </Field>
          <Field label="LOCATIONS (COMMA-SEPARATED)" name="locations" errors={fieldErrors} id="profiles-field-11">
            <input
              className="input"
              name="locations"
              aria-label="Locations"
              placeholder="Pune, Hyderabad"
              autoComplete="off"
             id="profiles-input-12"/>
          </Field>
          <Field label="KEY SKILLS (COMMA-SEPARATED)" name="requiredSkills" errors={fieldErrors} id="profiles-field-13">
            <input
              className="input"
              name="requiredSkills"
              aria-label="Key skills"
              placeholder="C#, ASP.NET Core"
              autoComplete="off"
             id="profiles-input-14"/>
          </Field>
          <div className="field-pair" id="profiles-div-15">
            <Field label="EXPERIENCE (YRS)" name="experienceYears" errors={fieldErrors} id="profiles-field-16">
              <input
                className="input"
                name="experienceYears"
                aria-label="Experience years"
                inputMode="numeric"
               id="profiles-input-17"/>
            </Field>
            <Field label="POSTED WITHIN (DAYS)" name="postedWithinDays" errors={fieldErrors} id="profiles-field-18">
              <input
                className="input"
                name="postedWithinDays"
                aria-label="Posted within days"
                inputMode="numeric"
               id="profiles-input-19"/>
            </Field>
          </div>
          <label className="check" id="profiles-label-20">
            <input type="checkbox" name="remoteOnly" aria-label="Remote only"  id="profiles-input-21"/> REMOTE ONLY
          </label>
          <button type="submit" className="btn" disabled={creating}>
            {creating ? "SAVING…" : "CREATE PROFILE"}
          </button>
        </form>
        {createProblem !== null && (
          <p className="alert" role="alert" id="profiles-p-22">
            <CodeTag code={createProblem.code} tone={codeTone(createProblem.code)}  id="profiles-codetag-23"/>{" "}
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
       id="profiles-module-24">
        <button type="button" className="btn btn-ghost" onClick={onRefresh} disabled={loading}>
          {loading ? "LOADING…" : "REFRESH"}
        </button>
        {listProblem !== null && (
          <p className="alert" role="alert" id="profiles-p-25">
            <CodeTag code={listProblem.code} tone={codeTone(listProblem.code)}  id="profiles-codetag-26"/>{" "}
            {listProblem.title}
          </p>
        )}
        {profiles !== null && profiles.length === 0 && (
          <p className="status-line">No profiles yet. Create one to get started.</p>
        )}
        {profiles !== null && profiles.length > 0 && (
          <div className="tbl-wrap" style={{ marginTop: "10px" }} id="profiles-div-27">
            <table className="data" aria-label="Search profiles" id="profiles-table-28">
              <thead id="profiles-thead-29">
                <tr id="profiles-tr-30">
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
                    <td id="profiles-td-31">
                      <button type="button" className="row-btn" onClick={() => onSelect(p.id)}>
                        {p.name}
                      </button>
                    </td>
                    <td id="profiles-td-32">
                      <CodeTag code={p.sourceKey}  id="profiles-codetag-33"/>
                    </td>
                    <td>{p.keywords}</td>
                    <td>{p.remoteOnly ? "remote" : p.locations.join(", ")}</td>
                    <td>{`${p.maxPages}p · ${p.maxJobs}j · ${p.maxDetails}d`}</td>
                    <td id="profiles-td-34">
                      <CodeTag
                        code={p.enabled ? "ENABLED" : "DISABLED"}
                        tone={p.enabled ? "ok" : "warn"}
                       id="profiles-codetag-35"/>
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
            <div className="tbl-wrap" id="profiles-div-36">
              <table className="data" aria-label="Profile detail" id="profiles-table-37">
                <tbody id="profiles-tbody-38">
                  <DetailRow label="ID" value={selected.id}  id="profiles-detailrow-39"/>
                  <DetailRow label="Name" value={selected.name}  id="profiles-detailrow-40"/>
                  <DetailRow label="Source" value={selected.sourceKey}  id="profiles-detailrow-41"/>
                  <DetailRow label="Keywords" value={selected.keywords}  id="profiles-detailrow-42"/>
                  <DetailRow
                    label="Locations"
                    value={selected.locations.length > 0 ? selected.locations.join(", ") : "—"}
                  />
                  <DetailRow label="Experience" value={orDash(selected.experienceYears, " yrs")}  id="profiles-detailrow-43"/>
                  <DetailRow
                    label="Posted within"
                    value={orDash(selected.postedWithinDays, " days")}
                   id="profiles-detailrow-44"/>
                  <DetailRow label="Remote only" value={selected.remoteOnly ? "yes" : "no"}  id="profiles-detailrow-45"/>
                  <DetailRow
                    label="Key skills"
                    value={
                      selected.requiredSkills.length > 0 ? selected.requiredSkills.join(", ") : "—"
                    }
                  />
                  <DetailRow
                    label="Caps"
                    value={`${plural(selected.maxPages, "page")} · ${plural(selected.maxJobs, "job")} · ${plural(selected.maxDetails, "detail")}`}
                   id="profiles-detailrow-46"/>
                  <DetailRow
                    label="Delay window"
                    value={`${selected.minDelayMs}–${selected.maxDelayMs} ms`}
                   id="profiles-detailrow-47"/>
                  <DetailRow label="Created (UTC)" value={selected.createdAt}  id="profiles-detailrow-48"/>
                </tbody>
              </table>
            </div>
            <div style={{ marginTop: "10px" }} id="profiles-div-49">
              <JsonBlock label="Profile payload" value={selected}  id="profiles-jsonblock-50"/>
            </div>
          </>
        )}
      </Module>

      <Module
        code="PRF-04"
        title="Code reference"
        span="span-5"
        note="The contract for these endpoints. UI branches on code."
       id="profiles-module-51">
        <div className="tbl-wrap" id="profiles-div-52">
          <table className="data" id="profiles-table-53">
            <thead id="profiles-thead-54">
              <tr id="profiles-tr-55">
                <th>Code</th>
                <th>HTTP</th>
                <th>Meaning</th>
              </tr>
            </thead>
            <tbody id="profiles-tbody-56">
              <CodeRow code="OK" http="200/201" text="Listed, fetched, or created."  id="profiles-coderow-57"/>
              <CodeRow
                code="VALIDATION_FAILED"
                http="400"
                text="Field errors shown under each input."
               id="profiles-coderow-58"/>
              <CodeRow
                code="PROFILE_NAME_TAKEN"
                http="409"
                text="Another profile already uses that name."
               id="profiles-coderow-59"/>
              <CodeRow code="PROFILE_NOT_FOUND" http="404" text="No profile with that id."  id="profiles-coderow-60"/>
              <CodeRow
                code="DOTNET_UNREACHABLE"
                http="502"
                text="BFF alive, API down. Start the data plane."
               id="profiles-coderow-61"/>
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
    requiredSkills: text("requiredSkills")
      .split(",")
      .map((s) => s.trim())
      .filter((s) => s !== ""),
  };
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <tr id="profiles-tr-62">
      <th>{label}</th>
      <td>{value}</td>
    </tr>
  );
}

function CodeRow({ code, http, text }: { code: string; http: string; text: string }) {
  return (
    <tr id="profiles-tr-63">
      <td id="profiles-td-64">
        <CodeTag code={code} tone={codeTone(code)}  id="profiles-codetag-65"/>
      </td>
      <td>{http}</td>
      <td>{text}</td>
    </tr>
  );
}

function orDash(value: number | null, unit: string): string {
  return value === null ? "—" : `${value}${unit}`;
}
