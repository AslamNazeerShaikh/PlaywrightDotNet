import { useEffect, useRef, useState } from "react";

import { Abbr } from "../components/legend.tsx";
import { CodeTag, codeTone, constantCase, Field, Module, plural } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type {
  ApiClient,
  ApiProblem,
  RunPayload,
  RunPosting,
  SearchProfilePayload,
  SessionPayload,
} from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import type { LogEvent } from "../lib/telemetry.ts";
import { stateLabel } from "./sessions.tsx";
import { Meter } from "./sources.tsx";

/** How often an executing run is polled. The spec's SSE fallback cadence is 2–5 s. */
export const RUN_POLL_MS = 2000;

/** Lifecycle stages a healthy run walks through, in order. */
export const RUN_STAGES = [
  "Pending",
  "StartingSession",
  "Searching",
  "Enriching",
  "Persisting",
  "Completed",
] as const;

/**
 * Runs page: start a scrape for a saved search profile in one of its source's sessions, follow it
 * live, cancel it, and read the postings it found. The page owns its data: it loads runs and the
 * form's options once per visit, and polls only the selected run while it executes — one request
 * chain at a time (next poll after the last answer), paused while the tab is hidden, stopped once
 * the run is terminal.
 */
export function RunsPage({
  client,
  onEvent,
}: {
  client: ApiClient;
  onEvent: (event: LogEvent) => void;
}) {
  useRenderLog("RunsPage");
  const [runs, setRuns] = useState<RunPayload[] | null>(null);
  const [listProblem, setListProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);
  const [profiles, setProfiles] = useState<SearchProfilePayload[]>([]);
  const [sessions, setSessions] = useState<SessionPayload[]>([]);
  const [profileId, setProfileId] = useState("");
  const [starting, setStarting] = useState(false);
  const [startProblem, setStartProblem] = useState<ApiProblem | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [postings, setPostings] = useState<RunPosting[] | null>(null);
  const [cancelling, setCancelling] = useState(false);
  // Latest-wins for the history list, and a stable handle on the event
  // sink so the polling effect is keyed on the run alone.
  const listTicket = useRef(0);
  const onEventRef = useRef(onEvent);
  onEventRef.current = onEvent;

  async function loadRuns() {
    const ticket = ++listTicket.current;
    setLoading(true);
    const { result } = await client.listRuns();
    if (ticket !== listTicket.current) {
      return;
    }
    if (result.ok) {
      setRuns(result.value.items);
      setListProblem(null);
      setSelectedId((current) => current ?? result.value.items[0]?.id ?? null);
    } else {
      setListProblem(result.problem);
    }
    setLoading(false);
  }

  const loadRunsRef = useRef(loadRuns);
  loadRunsRef.current = loadRuns;

  // Once per visit: the history plus the form's options.
  useEffect(() => {
    let alive = true;
    void loadRunsRef.current();
    void Promise.all([client.listProfiles(), client.listSessions()]).then(([p, s]) => {
      if (!alive) {
        return;
      }
      if (p.result.ok) {
        const enabled = p.result.value.items.filter((x) => x.enabled);
        setProfiles(enabled);
        setProfileId((current) => current || (enabled[0]?.id ?? ""));
      }
      if (s.result.ok) {
        setSessions(s.result.value.items);
      }
    });
    return () => {
      alive = false;
    };
  }, [client]);

  // Follow the selected run: poll while it executes, fetch once when it
  // is already finished, and log the moment it ends.
  useEffect(() => {
    if (selectedId === null) {
      return;
    }
    let alive = true;
    let timer: ReturnType<typeof setTimeout> | undefined;
    let sawActive = false;
    const tick = async () => {
      if (document.visibilityState !== "hidden") {
        const [run, list] = await Promise.all([
          client.getRun(selectedId),
          client.listRunPostings(selectedId),
        ]);
        if (!alive) {
          return;
        }
        if (list.result.ok) {
          setPostings(list.result.value.items);
        }
        if (run.result.ok) {
          const current = run.result.value;
          setRuns((rs) => upsertRun(rs, current));
          if (current.isTerminal) {
            if (sawActive) {
              onEventRef.current(finishedEvent(current, run.at));
            }
            return;
          }
          sawActive = true;
        }
      }
      timer = setTimeout(() => void tick(), RUN_POLL_MS);
    };
    void tick();
    return () => {
      alive = false;
      clearTimeout(timer);
    };
  }, [client, selectedId]);

  const profile = profiles.find((p) => p.id === profileId) ?? null;
  const usable = sessions.filter((s) => s.sourceKey === profile?.sourceKey);
  const selected = runs?.find((r) => r.id === selectedId) ?? null;
  const fieldErrors = startProblem?.errors ?? {};

  async function start(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setStarting(true);
    const { result, at } = await client.startRun({
      profileId,
      browserProfileId: String(data.get("browserProfileId") ?? ""),
      enrich: data.get("enrich") !== null,
    });
    setStarting(false);
    if (!result.ok) {
      setStartProblem(result.problem);
      onEventRef.current({
        at: clock(at),
        kind: "err",
        code: result.problem.code,
        detail: result.problem.title,
      });
      return;
    }
    setStartProblem(null);
    setRuns((rs) => upsertRun(rs, result.value));
    setPostings(null);
    setSelectedId(result.value.id);
    onEventRef.current({
      at: clock(at),
      kind: "ok",
      code: "RUN_STARTED",
      detail: `${result.value.profileName ?? "profile"} · ${result.value.sourceKey}`,
    });
  }

  async function cancel(id: string) {
    setCancelling(true);
    const { result } = await client.cancelRun(id);
    setCancelling(false);
    if (result.ok) {
      setRuns((rs) => upsertRun(rs, result.value));
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // RUNS" />
        </h1>
        <p>
          Start a scrape, follow it live, and read what it found. One run at a time; caps and quotas
          come from the profile and the source policy.
        </p>
      </div>

      <Module
        code="RUN-01"
        title="Start a run"
        span="span-5"
        note={
          <>
            POST /api/v1/runs. A browser window opens: Naukri blocks{" "}
            <Abbr term="headless" full="a browser without a visible window" /> first contact.
          </>
        }
      >
        <form onSubmit={start} aria-label="Start a run" noValidate>
          <Field label="SEARCH PROFILE" name="profileId" errors={fieldErrors}>
            <select
              className="input"
              name="profileId"
              aria-label="Search profile"
              value={profileId}
              onChange={(e) => setProfileId(e.currentTarget.value)}
            >
              {profiles.length === 0 && <option value="">No enabled profiles</option>}
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>
                  {`${p.name} · ${p.sourceKey}`}
                </option>
              ))}
            </select>
          </Field>
          <Field label="SESSION" name="browserProfileId" errors={fieldErrors}>
            <select
              className="input"
              name="browserProfileId"
              aria-label="Session"
              key={profile?.sourceKey ?? "none"}
            >
              {usable.length === 0 && <option value="">No session for this source</option>}
              {usable.map((s) => (
                <option key={s.id} value={s.id} disabled={!s.canLaunch}>
                  {`${s.name} · ${s.engine}/${s.channel} · ${stateLabel(s.state)}`}
                </option>
              ))}
            </select>
          </Field>
          {profile !== null && <p className="status-line">{profileInputs(profile)}</p>}
          <label className="check">
            <input type="checkbox" name="enrich" aria-label="Enrich" defaultChecked /> VISIT DETAIL
            PAGES (DESCRIPTION, SKILLS)
          </label>
          <button
            type="submit"
            className="btn"
            disabled={starting || profile === null || usable.length === 0}
          >
            {starting ? "STARTING…" : "START RUN"}
          </button>
        </form>
        {profile !== null && usable.length === 0 && (
          <p className="status-line">
            Create a {profile.sourceKey} session on the Sessions page first.
          </p>
        )}
        {startProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={startProblem.code} tone={codeTone(startProblem.code)} />{" "}
            {startProblem.title}
            {startProblem.detail !== "" ? ` — ${startProblem.detail}` : ""}
          </p>
        )}
      </Module>

      <Module
        code="RUN-02"
        title="Run history"
        span="span-7"
        note="GET /api/v1/runs, newest first. Select a run to follow it."
      >
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => void loadRuns()}
          disabled={loading}
        >
          {loading ? "LOADING…" : "REFRESH"}
        </button>
        {listProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={listProblem.code} tone={codeTone(listProblem.code)} />{" "}
            {listProblem.title}
          </p>
        )}
        {runs !== null && runs.length === 0 && (
          <p className="status-line">No runs yet. Start one to see it here.</p>
        )}
        {runs !== null && runs.length > 0 && (
          <div className="tbl-wrap" style={{ marginTop: "10px" }}>
            <table className="data" aria-label="Runs">
              <thead>
                <tr>
                  <th>Started (UTC)</th>
                  <th>Profile</th>
                  <th>Stage</th>
                  <th>Pages</th>
                  <th>Found · new</th>
                  <th>Details</th>
                  <th>Took</th>
                </tr>
              </thead>
              <tbody>
                {runs.map((r) => (
                  <tr key={r.id} aria-selected={r.id === selectedId}>
                    <td>
                      <button
                        type="button"
                        className="row-btn"
                        onClick={() => {
                          setPostings(null);
                          setSelectedId(r.id);
                        }}
                      >
                        {stamp(r.startedAt ?? r.createdAt)}
                      </button>
                    </td>
                    <td>{`${r.profileName ?? "—"} · ${r.sourceKey}`}</td>
                    <td>
                      <CodeTag code={constantCase(r.stage)} tone={stageTone(r.stage)} />
                    </td>
                    <td>{`${r.pagesFetched}/${r.maxPages}`}</td>
                    <td>{`${r.postingsFound} · ${r.postingsNew}`}</td>
                    <td>{r.detailsFetched}</td>
                    <td>{runDuration(r, Date.now())}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Module>

      <Module
        code="RUN-03"
        title="Run detail"
        span="span-12"
        note={`Polled every ${RUN_POLL_MS / 1000} s while it executes; walls pause it, never retry.`}
      >
        {selected === null ? (
          <p className="status-line">Select a run to follow it.</p>
        ) : (
          <RunDetail
            run={selected}
            cancelling={cancelling}
            onCancel={() => void cancel(selected.id)}
          />
        )}
      </Module>

      <Module
        code="RUN-04"
        title="Postings"
        span="span-12"
        note="What the selected run saw, most recent first. Titles open the posting on the source."
      >
        {selected !== null && postings !== null && postings.length > 0 && (
          <p className="status-line">
            Export:{" "}
            <a href={`/api/v1/runs/${selected.id}/postings/export?format=csv`} download>
              CSV
            </a>{" "}
            ·{" "}
            <a href={`/api/v1/runs/${selected.id}/postings/export?format=xlsx`} download>
              XLSX
            </a>
          </p>
        )}
        {selected === null || postings === null ? (
          <p className="status-line">
            {selected === null ? "No run selected." : "Loading postings…"}
          </p>
        ) : postings.length === 0 ? (
          <p className="status-line">
            {selected.isTerminal ? "This run saw no postings." : "No postings yet."}
          </p>
        ) : (
          <div className="tbl-wrap">
            <table className="data" aria-label="Postings">
              <thead>
                <tr>
                  <th>Title</th>
                  <th>Company</th>
                  <th>Location</th>
                  <th>Experience</th>
                  <th>Salary</th>
                  <th>Posted</th>
                  <th>Skills</th>
                </tr>
              </thead>
              <tbody>
                {postings.map((p) => (
                  <tr key={p.id}>
                    <td>
                      <a href={p.url} target="_blank" rel="noopener noreferrer">
                        {p.title}
                      </a>{" "}
                      {p.isNew && <CodeTag code="NEW" tone="ok" />}
                    </td>
                    <td>{p.company ?? "—"}</td>
                    <td>{p.location ?? "—"}</td>
                    <td>{p.experience ?? "—"}</td>
                    <td>{p.salary ?? "—"}</td>
                    <td>{p.postedAt === null ? "—" : p.postedAt.slice(0, 10)}</td>
                    <td>{p.skills.slice(0, 4).join(", ") || "—"}</td>
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

function RunDetail({
  run,
  cancelling,
  onCancel,
}: {
  run: RunPayload;
  cancelling: boolean;
  onCancel: () => void;
}) {
  const reached = RUN_STAGES.indexOf(run.stage as (typeof RUN_STAGES)[number]);
  return (
    <>
      <div className="tag-row" role="list" aria-label="Run stages">
        {RUN_STAGES.map((stage, i) => (
          <span key={stage} role="listitem">
            <CodeTag
              code={constantCase(stage)}
              tone={stage === run.stage ? stageTone(run.stage) : i < reached ? "ok" : undefined}
            />
          </span>
        ))}
        {reached === -1 && (
          <span role="listitem">
            <CodeTag code={constantCase(run.stage)} tone={stageTone(run.stage)} />
          </span>
        )}
      </div>
      <p className="status-line" role="status">
        {`${run.profileName ?? "Deleted profile"} · ${run.sourceKey} · ${constantCase(run.stage)}`}
        {run.failureReason !== null ? ` — ${run.failureReason}` : ""}
        {run.pauseReason !== null ? ` — paused: ${run.pauseReason}` : ""}
        {run.retryAt !== null ? ` · retry after ${stamp(run.retryAt)} UTC` : ""}
      </p>
      <div className="field-pair">
        <Meter label="Pages" used={run.pagesFetched} cap={run.maxPages} progress />
        <Meter label="Postings" used={run.postingsFound} cap={run.maxJobs} progress />
        <Meter label="Details" used={run.detailsFetched} cap={run.maxDetails} progress />
      </div>
      <p className="status-line">
        {`${plural(run.postingsNew, "new posting")} · ${plural(run.postingsUpdated, "update")} · ${plural(run.challenges, "wall")} · ${plural(run.failures, "failure")}`}
      </p>
      {!run.isTerminal && (
        <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={cancelling}>
          {cancelling ? "CANCELLING…" : "CANCEL RUN"}
        </button>
      )}
    </>
  );
}

/** One line of what the chosen profile will search: keywords, place, and filters. */
export function profileInputs(profile: SearchProfilePayload): string {
  const place = profile.remoteOnly ? "remote" : profile.locations.join(", ") || "—";
  const experience =
    profile.experienceYears === null ? "any exp" : `${profile.experienceYears} yrs`;
  const recency =
    profile.postedWithinDays === null ? "any date" : `last ${profile.postedWithinDays}d`;
  const skills =
    profile.requiredSkills.length === 0
      ? "no skill filter"
      : `skills: ${profile.requiredSkills.join(", ")}`;
  return `Inputs: ${profile.keywords} · ${place} · ${experience} · ${recency} · ${skills}`;
}

/** Tone for a run stage tag: finished well, finished badly, waiting on a person, or moving. */
export function stageTone(stage: string): "ok" | "bad" | "warn" | undefined {
  if (stage === "Completed") {
    return "ok";
  }
  if (stage === "Failed") {
    return "bad";
  }
  if (stage === "Cancelled" || stage === "PausedForChallenge" || stage === "RateLimited") {
    return "warn";
  }
  return undefined;
}

/** Replaces a run by id, or puts a new one first (newest-first history). */
export function upsertRun(runs: RunPayload[] | null, run: RunPayload): RunPayload[] {
  const list = runs ?? [];
  return list.some((r) => r.id === run.id)
    ? list.map((r) => (r.id === run.id ? run : r))
    : [run, ...list];
}

/** How long a run took (or has taken so far), e.g. "42 s", "3 m 05 s"; "—" before it starts. */
export function runDuration(run: RunPayload, now: number): string {
  if (run.startedAt === null) {
    return "—";
  }
  const end = run.finishedAt === null ? now : Date.parse(run.finishedAt);
  const seconds = Math.max(0, Math.round((end - Date.parse(run.startedAt)) / 1000));
  return seconds < 60
    ? `${seconds} s`
    : `${Math.floor(seconds / 60)} m ${String(seconds % 60).padStart(2, "0")} s`;
}

function finishedEvent(run: RunPayload, at: string): LogEvent {
  return {
    at: clock(at),
    kind: run.stage === "Completed" ? "ok" : "err",
    code: constantCase(run.stage),
    detail: `${run.profileName ?? "run"} · ${run.postingsFound} found, ${run.postingsNew} new`,
  };
}

/** "2026-11-02 08:00:00" from an ISO timestamp. */
function stamp(iso: string): string {
  return iso.slice(0, 19).replace("T", " ");
}

function clock(iso: string): string {
  return iso.slice(11, 19);
}
