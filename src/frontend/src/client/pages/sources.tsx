import { CodeTag, Module, plural, RefreshButton } from "../components/modules.tsx";
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
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // SOURCES" />
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
      >
        <RefreshButton loading={loading} onRefresh={onRefresh} />
        {problem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={problem.code} tone="warn" /> {problem.title}
          </p>
        )}
      </Module>

      {sources?.items.map((source, i) => (
        <SourceCard
          key={source.key}
          source={source}
          code={`SRC-${String(i + 1).padStart(2, "0")}`}
        />
      ))}
    </>
  );
}

function SourceCard({ source, code }: { source: SourceStatus; code: string }) {
  const { policy, quota, circuit } = source;
  return (
    <Module code={code} title={source.key.toUpperCase()} span="span-4">
      <div className="tag-row" aria-label={`${source.key} status`}>
        <CodeTag code={source.tier.toUpperCase()} tone={tierTone(source.tier)} />
        <CodeTag
          code={source.enabled ? "ENABLED" : "DISABLED"}
          tone={source.enabled ? "ok" : "warn"}
        />
        <CodeTag
          code={source.gate.allowed ? "GATE OPEN" : "GATE BLOCKED"}
          tone={source.gate.allowed ? "ok" : "bad"}
        />
        <CodeTag
          code={`CIRCUIT ${circuit.state.toUpperCase()}`}
          tone={circuitTone(circuit.state)}
        />
      </div>
      {source.gate.reason !== null && <p className="status-line">{source.gate.reason}</p>}

      <p className="module-note">TODAY · {quota.date} UTC</p>
      <Meter label="Pages" used={quota.pagesUsed} cap={quota.pageCap} />
      <Meter label="Details" used={quota.detailsUsed} cap={quota.detailCap} />
      {quota.runCap === null ? (
        <p className="status-line">Runs: {quota.runsUsed} · no daily cap</p>
      ) : (
        <Meter label="Runs" used={quota.runsUsed} cap={quota.runCap} />
      )}

      <div className="tbl-wrap">
        <table className="data" aria-label={`${source.key} policy`}>
          <tbody>
            <tr>
              <th>Per run</th>
              <td>{`${plural(policy.maxPages, "page")} · ${plural(policy.maxJobs, "job")} · ${plural(policy.maxDetails, "detail")}`}</td>
            </tr>
            <tr>
              <th>Delay window</th>
              <td>{`${policy.minDelayMs}–${policy.maxDelayMs} ms`}</td>
            </tr>
            <tr>
              <th>Wall cooldown</th>
              <td>{`${policy.challengeCooldownMinutes} min`}</td>
            </tr>
            <tr>
              <th>Walls today</th>
              <td>{quota.challenges}</td>
            </tr>
            <tr>
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
  // A zero quota cap means blocked for the day, so the bar reads full.
  // A zero run cap means that step is switched off, so the bar reads empty.
  const ratio = cap <= 0 ? (progress ? 0 : 1) : Math.min(used / cap, 1);
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
    >
      <span>{label.toUpperCase()}</span>
      <span>{`${used} / ${cap}`}</span>
      <div className="meter-track">
        <div className="meter-bar" style={{ width: `${Math.round(ratio * 100)}%` }} />
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
