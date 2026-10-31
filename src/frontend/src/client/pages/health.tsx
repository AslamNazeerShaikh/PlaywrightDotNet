import { CodeTag, JsonBlock, Module, Stat } from "../components/modules.tsx";
import { Sparkline } from "../components/sparkline.tsx";
import type { ApiProblem, HealthPayload } from "../lib/api.ts";
import { formatMs, type LatencyStats, type Sample } from "../lib/telemetry.ts";

/**
 * Engine health page: the probe control, full-fidelity payload, latency statistics, sample history,
 * and what the numbers mean. Separate page per feature — the overview stays a summary.
 */
export function HealthPage({
  health,
  problem,
  checking,
  samples,
  stats,
  onCheck,
}: {
  health: HealthPayload | null;
  problem: ApiProblem | null;
  checking: boolean;
  samples: Sample[];
  stats: LatencyStats | null;
  onCheck: () => void;
}) {
  return (
    <>
      <div className="page-head">
        <h1>OPS-DECK // ENGINE HEALTH</h1>
        <p>Liveness of the .NET data plane, measured through the BFF proxy.</p>
      </div>

      <Module
        code="HLT-01"
        title="Probe control"
        span="span-4"
        note="Each run appends a latency sample and an event-log row."
      >
        <button type="button" className="btn" onClick={onCheck} disabled={checking}>
          {checking ? "PROBING…" : "RUN HEALTH CHECK"}
        </button>
        {health !== null && (
          <p className="status-line" role="status" style={{ marginTop: "10px" }}>
            ENGINE <CodeTag code={health.status.toUpperCase()} tone="ok" /> @ {health.timeUtc}
          </p>
        )}
        {problem !== null && (
          <p className="alert" role="alert">
            <CodeTag code={problem.code} tone="bad" /> {problem.title}
            {problem.detail !== "" ? ` — ${problem.detail}` : ""}
          </p>
        )}
      </Module>

      <Module
        code="HLT-02"
        title="Latency"
        span="span-4"
        note="Rolling window of the last 60 probes."
      >
        <Sparkline samples={samples} label="Health probe latency" />
        <div className="stat-grid" style={{ marginTop: "10px" }}>
          <Stat label="Probes" value={stats === null ? "0" : String(stats.count)} />
          <Stat
            label="Last"
            value={stats === null ? "—" : formatMs(stats.last)}
            tone={stats ? "ok" : undefined}
          />
          <Stat label="p50" value={stats === null ? "—" : formatMs(stats.p50)} />
          <Stat label="Min" value={stats === null ? "—" : formatMs(stats.min)} />
          <Stat label="Max" value={stats === null ? "—" : formatMs(stats.max)} />
        </div>
      </Module>

      <Module
        code="HLT-03"
        title="Probe history"
        span="span-4"
        note="Newest last, mirroring the chart left-to-right."
      >
        {samples.length === 0 ? (
          <p className="status-line">No probes yet.</p>
        ) : (
          <div className="tbl-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>#</th>
                  <th>Round trip</th>
                  <th>Outcome</th>
                </tr>
              </thead>
              <tbody>
                {samples.slice(-12).map((s, i) => (
                  <tr key={`${s.at}-${i}`}>
                    <td>{samples.length - Math.min(samples.length, 12) + i + 1}</td>
                    <td>{formatMs(s.ms)}</td>
                    <td>
                      <CodeTag code={s.ok ? "OK" : "FAIL"} tone={s.ok ? "ok" : "bad"} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Module>

      <Module
        code="HLT-04"
        title="Raw payload"
        span="span-6"
        note="Exactly what the API returned — the deck renders fields, never prose."
      >
        {health === null ? (
          <p className="status-line">Run a probe to capture the payload.</p>
        ) : (
          <JsonBlock label="Health payload" value={health} />
        )}
      </Module>

      <Module
        code="HLT-05"
        title="Reading the dials"
        span="span-6"
        note="What each field guarantees."
      >
        <div className="tbl-wrap">
          <table className="data">
            <thead>
              <tr>
                <th>Signal</th>
                <th>Meaning</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>status = healthy</td>
                <td>API process alive, JSON pipeline intact, clock readable.</td>
              </tr>
              <tr>
                <td>timeUtc</td>
                <td>
                  Server clock at response time; drift against the deck clock hints at environment
                  skew.
                </td>
              </tr>
              <tr>
                <td>round trip</td>
                <td>
                  Browser → BFF → API → back. Spikes implicate the proxy path, not just the API.
                </td>
              </tr>
              <tr>
                <td>502 + DOTNET_UNREACHABLE</td>
                <td>BFF alive, API down or unreachable. Start the API, not the BFF.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </Module>
    </>
  );
}
