import { useEffect, useRef, useState } from "react";

import { InfoTip } from "../components/info-tip.tsx";
import { Abbr } from "../components/legend.tsx";
import {
  CodeTag,
  codeTone,
  constantCase,
  Field,
  Module,
  plural,
  RefreshButton,
} from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type {
  ApiClient,
  ApiProblem,
  ExportFormat,
  ExportGroup,
  RunPayload,
  RunPosting,
  SearchProfilePayload,
  SessionPayload,
} from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import { formatClock } from "../lib/format.ts";
import type { LogEvent } from "../lib/telemetry.ts";
import { stateLabel } from "./sessions.tsx";
import { Meter } from "./sources.tsx";

/** How often an executing run is polled. The spec's SSE fallback cadence is 2–5 s. */
export const RUN_POLL_MS = 2000;

/** Runs history page size, mirroring the database grid's LIMIT. */
export const RUNS_LIMIT = 50;

export const POSTINGS_LIMIT = 200;

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
  const [runsOffset, setRunsOffset] = useState(0);
  const [runsTotal, setRunsTotal] = useState(0);
  const [listProblem, setListProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);
  const [profiles, setProfiles] = useState<SearchProfilePayload[]>([]);
  const [sessions, setSessions] = useState<SessionPayload[]>([]);
  const [profileId, setProfileId] = useState("");
  const [starting, setStarting] = useState(false);
  const [startProblem, setStartProblem] = useState<ApiProblem | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [postings, setPostings] = useState<RunPosting[] | null>(null);
  const [postingsOffset, setPostingsOffset] = useState(0);
  const [postingsTotal, setPostingsTotal] = useState(0);
  const [exportProblem, setExportProblem] = useState<ApiProblem | null>(null);
  const [exporting, setExporting] = useState<string | null>(null);
  const [cancelling, setCancelling] = useState(false);
  // Latest-wins for the history list, and a stable handle on the event
  // sink so the polling effect is keyed on the run alone.
  const listTicket = useRef(0);
  const onEventRef = useRef(onEvent);
  onEventRef.current = onEvent;

  async function loadRuns() {
    const ticket = ++listTicket.current;
    setLoading(true);
    const { result } = await client.listRuns(runsOffset, RUNS_LIMIT);
    if (ticket !== listTicket.current) {
      return;
    }
    if (result.ok) {
      setRuns(result.value.items);
      setRunsTotal(result.value.total);
      setListProblem(null);
      setSelectedId((current) => current ?? result.value.items[0]?.id ?? null);
    } else {
      setListProblem(result.problem);
    }
    setLoading(false);
  }

  const loadRunsRef = useRef(loadRuns);
  loadRunsRef.current = loadRuns;

  function select(id: string) {
    setPostings(null);
    setPostingsOffset(0);
    setPostingsTotal(0);
    setExportProblem(null);
    setSelectedId(id);
  }

  // Once per visit: the run form's options.
  useEffect(() => {
    let alive = true;
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

  // The history list, re-read on page change.
  useEffect(() => {
    void loadRunsRef.current();
  }, [client, runsOffset]);

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
          client.listRunPostings(selectedId, postingsOffset, POSTINGS_LIMIT),
        ]);
        if (!alive) {
          return;
        }
        if (list.result.ok) {
          setPostings(list.result.value.items);
          setPostingsTotal(list.result.value.total);
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
  }, [client, selectedId, postingsOffset]);

  const profile = profiles.find((p) => p.id === profileId) ?? null;
  const usable = sessions.filter((s) => s.sourceKey === profile?.sourceKey);
  const selected = runs?.find((r) => r.id === selectedId) ?? null;
  const selectedCompanies = selected?.companies ?? [];
  const fieldErrors = startProblem?.errors ?? {};

  async function start(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setStarting(true);
    const { result, at } = await client.startRun({
      profileId,
      browserProfileId: String(data.get("browserProfileId") ?? ""),
      enrich: data.get("enrich") !== null,
      onlyNew: data.get("onlyNew") !== null,
    });
    setStarting(false);
    if (!result.ok) {
      setStartProblem(result.problem);
      onEventRef.current({
        at: formatClock(at),
        kind: "err",
        code: result.problem.code,
        detail: result.problem.title,
      });
      return;
    }
    setStartProblem(null);
    setRuns((rs) => upsertRun(rs, result.value));
    setPostings(null);
    setPostingsOffset(0);
    setPostingsTotal(0);
    setSelectedId(result.value.id);
    onEventRef.current({
      at: formatClock(at),
      kind: "ok",
      code: "RUN_STARTED",
      detail: `${result.value.profileName ?? "profile"} · ${result.value.sourceKey}`,
    });
  }

  async function exportRun(format: ExportFormat, group: ExportGroup = "all") {
    if (selected === null) {
      return;
    }
    const key = `${format}:${group}`;
    setExporting(key);
    try {
      const { result, at } = await client.downloadRunExport(selected.id, format, group);
      if (!result.ok) {
        setExportProblem(result.problem);
        onEventRef.current({
          at: formatClock(at),
          kind: "err",
          code: result.problem.code,
          detail: result.problem.title,
        });
        return;
      }
      setExportProblem(null);
      const url = URL.createObjectURL(result.value.blob);
      try {
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = result.value.filename;
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
      } finally {
        URL.revokeObjectURL(url);
      }
      onEventRef.current({
        at: formatClock(at),
        kind: "ok",
        code: "EXPORTED",
        detail: result.value.filename,
      });
    } finally {
      setExporting(null);
    }
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
            PAGES (DESCRIPTION, SKILLS){" "}
            <InfoTip text="Opens each detail page to fill description and skills." />
          </label>
          <label className="check">
            <input type="checkbox" name="onlyNew" aria-label="New only" /> NEW ONLY{" "}
            <InfoTip text="Keeps only postings first seen in this run." />
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
        <RefreshButton loading={loading} onRefresh={() => void loadRuns()} />
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
          <div className="tbl-wrap" style={{ marginTop: "var(--gap)" }}>
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
                      <button type="button" className="row-btn" onClick={() => select(r.id)}>
                        {stamp(r.startedAt ?? r.createdAt)}
                      </button>
                    </td>
                    <td>{`${r.profileName ?? "—"} · ${r.sourceKey}`}</td>
                    <td>
                      <CodeTag code={constantCase(r.stage)} tone={stageTone(r.stage)} />
                    </td>
                    <td>{`${r.pagesFetched}/${r.maxPages}${pageWindow(r.startPage, r.maxPages)}`}</td>
                    <td>{`${r.postingsFound} · ${r.postingsNew}`}</td>
                    <td>{r.detailsFetched}</td>
                    <td>{runDuration(r, Date.now())}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {runs !== null && runs.length > 0 && (
          <p className="status-line">
            <button
              type="button"
              className="btn btn-ghost"
              disabled={runsOffset === 0 || loading}
              onClick={() => setRunsOffset(Math.max(0, runsOffset - RUNS_LIMIT))}
            >
              PREV
            </button>{" "}
            showing {runs.length} of {runsTotal}{" "}
            <button
              type="button"
              className="btn btn-ghost"
              disabled={runsOffset + RUNS_LIMIT >= runsTotal || loading}
              onClick={() => setRunsOffset(runsOffset + RUNS_LIMIT)}
            >
              NEXT
            </button>
          </p>
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
        note={
          <>
            {selected?.onlyNew
              ? "New only: the list holds postings first seen in this run, most recent first. Titles open the posting on the source."
              : "What the selected run saw, most recent first. Titles open the posting on the source."}{" "}
            <InfoTip text="NEW marks first-seen rows; exports write the rows shown." />
          </>
        }
      >
        {selected !== null && postings !== null && postings.length > 0 && (
          <>
            <p className="status-line">
              Export all:{" "}
              <ExportButton
                label="CSV"
                ariaLabel="Export all as CSV"
                busy={exporting !== null}
                onExport={() => void exportRun("csv")}
              />{" "}
              ·{" "}
              <ExportButton
                label="XLSX"
                ariaLabel="Export all as XLSX"
                busy={exporting !== null}
                onExport={() => void exportRun("xlsx")}
              />
            </p>
            {selectedCompanies.length > 0 && (
              <p className="status-line">
                Company filter ({selectedCompanies.length}{" "}
                {selectedCompanies.length === 1 ? "company" : "companies"}): Only:{" "}
                <ExportButton
                  label="CSV"
                  ariaLabel="Export only companies as CSV"
                  busy={exporting !== null}
                  onExport={() => void exportRun("csv", "only")}
                />{" "}
                ·{" "}
                <ExportButton
                  label="XLSX"
                  ariaLabel="Export only companies as XLSX"
                  busy={exporting !== null}
                  onExport={() => void exportRun("xlsx", "only")}
                />
                {" · "}
                Exclude:{" "}
                <ExportButton
                  label="CSV"
                  ariaLabel="Export exclude companies as CSV"
                  busy={exporting !== null}
                  onExport={() => void exportRun("csv", "exclude")}
                />{" "}
                ·{" "}
                <ExportButton
                  label="XLSX"
                  ariaLabel="Export exclude companies as XLSX"
                  busy={exporting !== null}
                  onExport={() => void exportRun("xlsx", "exclude")}
                />
              </p>
            )}
          </>
        )}
        {exportProblem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={exportProblem.code} tone={codeTone(exportProblem.code)} />{" "}
            {exportProblem.title}
            {exportProblem.detail !== "" ? ` — ${exportProblem.detail}` : ""}
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
          <>
            <div className="tbl-wrap">
              <table className="data" aria-label="Postings">
                <thead>
                  <tr>
                    <th className="rank-cell">#</th>
                    <th>Title</th>
                    <th>Company</th>
                    <th>Location</th>
                    <th>Experience</th>
                    <th>Salary</th>
                    <th className="posted-cell">Posted</th>
                    <th>Skills</th>
                  </tr>
                </thead>
                <tbody>
                  {postings.map((p, i) => (
                    <tr key={p.id}>
                      <td className="rank-cell">{postingsOffset + i + 1}</td>
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
                      <td className="posted-cell">
                        {p.postedAt === null ? "—" : p.postedAt.slice(0, 10)}
                      </td>
                      <td>{p.skills.slice(0, 4).join(", ") || "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="status-line">
              <button
                type="button"
                className="btn btn-ghost"
                disabled={postingsOffset === 0}
                onClick={() => setPostingsOffset(Math.max(0, postingsOffset - POSTINGS_LIMIT))}
              >
                PREV
              </button>{" "}
              {postingsTotal === 0
                ? `0 of ${plural(postingsTotal, "posting")}`
                : `${postingsOffset + 1}–${Math.min(postingsOffset + POSTINGS_LIMIT, postingsTotal)} of ${plural(postingsTotal, "posting")}`}{" "}
              <button
                type="button"
                className="btn btn-ghost"
                disabled={postingsOffset + POSTINGS_LIMIT >= postingsTotal}
                onClick={() => setPostingsOffset(postingsOffset + POSTINGS_LIMIT)}
              >
                NEXT
              </button>
            </p>
          </>
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
        {run.onlyNew && (
          <span role="listitem">
            <CodeTag code="NEW ONLY" tone="ok" />
            <InfoTip text="This run kept only postings first seen in its window." />
          </span>
        )}
      </div>
      <p className="status-line" role="status">
        {`${run.profileName ?? "Deleted profile"} · ${run.sourceKey} · ${constantCase(run.stage)}${pageWindow(run.startPage, run.maxPages)}`}
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
        {`${plural(run.postingsNew, "new posting")} · ${plural(run.postingsUpdated, "update")} · ${plural(run.challenges, "wall")} · ${plural(run.failures, "failure")}`}{" "}
        <InfoTip text="new = first seen in this run · update = seen before · wall = challenge page · failure = broken page" />
      </p>
      {!run.isTerminal && (
        <button type="button" className="btn btn-ghost" onClick={onCancel} disabled={cancelling}>
          {cancelling ? "CANCELLING…" : "CANCEL RUN"}
        </button>
      )}
    </>
  );
}

/** One line of what the chosen profile will search: keywords, place, filters, and page window. */
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
  return `Inputs: ${profile.keywords} · ${place} · ${experience} · ${recency} · ${skills}${pageWindow(profile.startPage, profile.maxPages, " · pages ")}`;
}

/**
 * Page window suffix for runs and profiles past page one, e.g. " · pages 31–60". A missing start
 * page means 1: payloads written before the window existed.
 */
export function pageWindow(
  startPage: number | null | undefined,
  maxPages: number,
  prefix = " · ",
): string {
  const first = startPage ?? 1;
  if (!Number.isFinite(first) || !Number.isFinite(maxPages) || maxPages < 1) {
    return "";
  }
  return first > 1 ? `${prefix}${first}–${first + maxPages - 1}` : "";
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
    at: formatClock(at),
    kind: run.stage === "Completed" ? "ok" : "err",
    code: constantCase(run.stage),
    detail: `${run.profileName ?? "run"} · ${run.postingsFound} found, ${run.postingsNew} new`,
  };
}

/** Small text button for the export row; labels stay short because aria-labels carry the meaning. */
function ExportButton({
  label,
  ariaLabel,
  busy,
  onExport,
}: {
  label: string;
  ariaLabel: string;
  busy: boolean;
  onExport: () => void;
}) {
  return (
    <button
      type="button"
      className="row-btn"
      aria-label={ariaLabel}
      disabled={busy}
      onClick={onExport}
    >
      {label}
    </button>
  );
}

/** "2026-11-02 08:00:00" from an ISO timestamp. */
function stamp(iso: string): string {
  return iso.slice(0, 19).replace("T", " ");
}
