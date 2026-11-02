import { CodeTag, Module, plural } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiProblem, SourceList, SourceStatus } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";

/**
 * Sources page: one card per configured job source with its compliance tier and gate, today's quota
 * usage against the daily caps, the policy caps new profiles start from, and the circuit breaker.
 * Read-only — enabling a source is a configuration decision, never a click.
 */
export function SourcesPage({
  sources,
  problem,
  loading,
  onRefresh,
}: {
  sources: SourceList | null;
  problem: ApiProblem | null;
  loading: boolean;
  onRefresh: () => void;
}) {
  useRenderLog("SourcesPage");
  return (
    <>
      <div className="page-head" id="sources-div-1">
        <h1 id="sources-h1-2">
          <Scramble text="OPS-DECK // SOURCES"  id="sources-scramble-3"/>
        </h1>
        <p>
          Compliance gates, daily quota, and circuit state per source.
          {sources !== null ? ` Snapshot ${sources.asOf} (UTC).` : ""}
        </p>
      </div>

      <Module
        code="SRC-00"
        title="Snapshot"
        span="span-12"
        note="GET /api/v1/sources. Enabling a source is configuration, not a click."
       id="sources-module-4">
        <button type="button" className="btn btn-ghost" onClick={onRefresh} disabled={loading}>
          {loading ? "LOADING…" : "REFRESH"}
        </button>
        {problem !== null && (
          <p className="alert" role="alert" id="sources-p-5">
            <CodeTag code={problem.code} tone="warn"  id="sources-codetag-6"/> {problem.title}
          </p>
        )}
      </Module>

      {sources?.items.map((source, i) => (
        <SourceCard
          key={source.key}
          source={source}
          code={`SRC-${String(i + 1).padStart(2, "0")}`}
         id="sources-sourcecard-7"/>
      ))}
    </>
  );
}

function SourceCard({ source, code }: { source: SourceStatus; code: string }) {
  const { policy, quota, circuit } = source;
  return (
    <Module code={code} title={source.key.toUpperCase()} span="span-4" id="sources-module-8">
      <div className="tag-row" aria-label={`${source.key} status`} id="sources-div-9">
        <CodeTag code={source.tier.toUpperCase()} tone={tierTone(source.tier)}  id="sources-codetag-10"/>
        <CodeTag
          code={source.enabled ? "ENABLED" : "DISABLED"}
          tone={source.enabled ? "ok" : "warn"}
         id="sources-codetag-11"/>
        <CodeTag
          code={source.gate.allowed ? "GATE OPEN" : "GATE BLOCKED"}
          tone={source.gate.allowed ? "ok" : "bad"}
         id="sources-codetag-12"/>
        <CodeTag
          code={`CIRCUIT ${circuit.state.toUpperCase()}`}
          tone={circuitTone(circuit.state)}
         id="sources-codetag-13"/>
      </div>
      {source.gate.reason !== null && <p className="status-line">{source.gate.reason}</p>}

      <h3 className="module-note">TODAY · {quota.date} UTC</h3>
      <Meter label="Pages" used={quota.pagesUsed} cap={quota.pageCap}  id="sources-meter-14"/>
      <Meter label="Details" used={quota.detailsUsed} cap={quota.detailCap}  id="sources-meter-15"/>
      <Meter label="Runs" used={quota.runsUsed} cap={quota.runCap}  id="sources-meter-16"/>

      <div className="tbl-wrap" id="sources-div-17">
        <table className="data" aria-label={`${source.key} policy`} id="sources-table-18">
          <tbody id="sources-tbody-19">
            <tr id="sources-tr-20">
              <th>Per run</th>
              <td>{`${plural(policy.maxPages, "page")} · ${plural(policy.maxJobs, "job")} · ${plural(policy.maxDetails, "detail")}`}</td>
            </tr>
            <tr id="sources-tr-21">
              <th>Delay window</th>
              <td>{`${policy.minDelayMs}–${policy.maxDelayMs} ms`}</td>
            </tr>
            <tr id="sources-tr-22">
              <th>Wall cooldown</th>
              <td>{`${policy.challengeCooldownMinutes} min`}</td>
            </tr>
            <tr id="sources-tr-23">
              <th>Walls today</th>
              <td>{quota.challenges}</td>
            </tr>
            <tr id="sources-tr-24">
              <th>Circuit</th>
              <td>{circuitText(circuit)}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </Module>
  );
}

/** One daily cap as a labeled bar; a zero cap means the source is blocked for the day. */
export function Meter({
  label,
  used,
  cap,
  progress = false,
}: {
  label: string;
  used: number;
  cap: number;
  /** Progress toward a goal (a run's caps): reaching the cap is done, not a warning. */
  progress?: boolean;
}) {
  const ratio = cap <= 0 ? 1 : Math.min(used / cap, 1);
  const level = progress
    ? ratio >= 1
      ? "done"
      : "ok"
    : ratio >= 1
      ? "full"
      : ratio >= 0.8
        ? "near"
        : "ok";
  return (
    <div
      className="meter"
      data-level={level}
      role="meter"
      aria-label={`${label} used`}
      aria-valuemin={0}
      aria-valuemax={cap}
      aria-valuenow={used}
     id="sources-div-25">
      <span>{label.toUpperCase()}</span>
      <span>{`${used} / ${cap}`}</span>
      <div className="meter-track" id="sources-div-26">
        <div className="meter-bar" style={{ width: `${Math.round(ratio * 100)}%` }}  id="sources-div-27"/>
      </div>
    </div>
  );
}

export function circuitText(circuit: SourceStatus["circuit"]): string {
  if (circuit.state === "Closed") {
    return circuit.recentFailures === 0
      ? "closed · no recent walls"
      : `closed · ${circuit.recentFailures} wall(s) in 24 h`;
  }
  if (circuit.state === "HalfOpen") {
    return "half-open · one probe run allowed";
  }
  return circuit.canProbe
    ? "open · cooldown over, probe allowed"
    : `open until ${circuit.openUntil ?? "unknown"}`;
}

function tierTone(tier: SourceStatus["tier"]): "ok" | "warn" | "bad" {
  return tier === "Standard" ? "ok" : tier === "Restricted" ? "warn" : "bad";
}

function circuitTone(state: SourceStatus["circuit"]["state"]): "ok" | "warn" | "bad" {
  return state === "Closed" ? "ok" : state === "HalfOpen" ? "warn" : "bad";
}
