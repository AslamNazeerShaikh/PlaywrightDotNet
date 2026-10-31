import { useState } from "react";

import { Abbr } from "../components/legend.tsx";
import { CodeTag, codeTone, JsonBlock, Module } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiProblem, SearchProfilePayload } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";

/**
 * Search-profile lookup page: uncontrolled lookup form (no per-keystroke state, happy-dom-safe),
 * full result detail, lookup history, and the stable error-code reference. The store starts empty
 * in a fresh API process — a 404 with PROFILE_NOT_FOUND is the honest steady state until seeding
 * lands.
 */
export interface LookupRecord {
  id: string;
  at: string;
  outcome: "ok" | "err";
  code: string;
}

export function ProfilesPage({
  profile,
  problem,
  looking,
  history,
  onLookup,
}: {
  profile: SearchProfilePayload | null;
  problem: ApiProblem | null;
  looking: boolean;
  history: LookupRecord[];
  onLookup: (id: string) => void;
}) {
  useRenderLog("ProfilesPage");
  const [error, setError] = useState<ApiProblem | null>(null);

  function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const raw = String(new FormData(event.currentTarget).get("profileId") ?? "").trim();
    if (raw === "") {
      setError({
        status: 400,
        title: "Profile id required.",
        detail: "Enter a search-profile id to look up.",
        code: "VALIDATION_FAILED",
      });
      return;
    }
    setError(null);
    onLookup(raw);
  }

  const shown = error ?? problem;

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // SEARCH PROFILES" />
        </h1>
        <p>Point lookup by id. Outcomes key on stable codes, never message text.</p>
      </div>

      <Module
        code="PRF-01"
        title="Lookup"
        span="span-4"
        note={
          <>
            GET /api/v1/search-profiles/{"{id}"} through the{" "}
            <Abbr term="BFF" full="Backend for Frontend" /> proxy.
          </>
        }
      >
        <form onSubmit={submit}>
          <label className="field">
            <span>PROFILE ID</span>
            <input
              className="input"
              name="profileId"
              aria-label="Profile id"
              placeholder="00000000-0000-0000-0000-000000000000"
              autoComplete="off"
              spellCheck={false}
            />
          </label>
          <button type="submit" className="btn" disabled={looking}>
            {looking ? "QUERYING…" : "RUN LOOKUP"}
          </button>
        </form>
        {shown !== null && (
          <p className="alert" role="alert">
            <CodeTag code={shown.code} tone={codeTone(shown.code)} /> {shown.title}
            {shown.detail !== "" ? ` — ${shown.detail}` : ""}
          </p>
        )}
      </Module>

      <Module
        code="PRF-02"
        title="Result detail"
        span="span-4"
        note="Field table plus the exact payload. Empty until a 200 lands."
      >
        {profile === null ? (
          <p className="status-line">No profile loaded. A successful lookup fills this panel.</p>
        ) : (
          <>
            <div className="tbl-wrap">
              <table className="data">
                <tbody>
                  <tr>
                    <th>ID</th>
                    <td>{profile.id}</td>
                  </tr>
                  <tr>
                    <th>Name</th>
                    <td>{profile.name}</td>
                  </tr>
                  <tr>
                    <th>Source</th>
                    <td>
                      <CodeTag code={profile.sourceKey} tone="ok" />
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div style={{ marginTop: "10px" }}>
              <JsonBlock label="Profile payload" value={profile} />
            </div>
          </>
        )}
      </Module>

      <Module
        code="PRF-03"
        title="Code reference"
        span="span-4"
        note="The full contract for this endpoint. UI branches on code."
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
              <tr>
                <td>
                  <CodeTag code="OK" tone="ok" />
                </td>
                <td>200</td>
                <td>Profile exists; body is the SearchProfileResponse.</td>
              </tr>
              <tr>
                <td>
                  <CodeTag code="PROFILE_NOT_FOUND" tone="bad" />
                </td>
                <td>404</td>
                <td>No profile with that id. Steady state on a fresh store.</td>
              </tr>
              <tr>
                <td>
                  <CodeTag code="VALIDATION_FAILED" tone="warn" />
                </td>
                <td>400</td>
                <td>Empty or malformed id; nothing reached the store.</td>
              </tr>
              <tr>
                <td>
                  <CodeTag code="DOTNET_UNREACHABLE" tone="warn" />
                </td>
                <td>502</td>
                <td>BFF alive, API down. Check the data plane.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </Module>

      <Module
        code="PRF-04"
        title="Lookup history"
        span="span-12"
        note="Session-local trail of every lookup and its outcome code."
      >
        {history.length === 0 ? (
          <p className="status-line">No lookups yet this session.</p>
        ) : (
          <div className="tbl-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>At (UTC)</th>
                  <th>Queried id</th>
                  <th>Outcome</th>
                </tr>
              </thead>
              <tbody>
                {history.map((h, i) => (
                  <tr key={`${h.at}-${i}`}>
                    <td>{h.at}</td>
                    <td>{h.id}</td>
                    <td>
                      <CodeTag code={h.code} tone={codeTone(h.code)} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Module>
    </>
  );
}
