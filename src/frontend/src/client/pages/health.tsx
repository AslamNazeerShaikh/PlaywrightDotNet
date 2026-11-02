import { Abbr, Dot, Legend } from "../components/legend.tsx";
import { CodeTag, JsonBlock, Module, Stat } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import { Sparkline } from "../components/sparkline.tsx";
import type { ApiProblem, HealthPayload } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
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
  useRenderLog("HealthPage");
  return (
    <>
      <div className="page-head" id="health-div-1">
        <h1 id="health-h1-2">
          <Scramble text="OPS-DECK // ENGINE HEALTH"  id="health-scramble-3"/>
        </h1>
        <p>Liveness of the .NET data plane, measured through the BFF proxy.</p>
      </div>

      <Module
        code="HLT-01"
        title="Probe control"
        span="span-4"
        note="Auto-probes every 10s from mount; manual runs ride the same path. Each run appends a latency sample and an event-log row."
       id="health-module-4">
        <button type="button" className="btn" onClick={onCheck} disabled={checking}>
          {checking ? "PROBING…" : "RUN HEALTH CHECK"}
        </button>
        {health !== null && (
          <p className="status-line" role="status" style={{ marginTop: "10px" }}>
            ENGINE <CodeTag code={health.status.toUpperCase()} tone="ok"  id="health-codetag-5"/> @ {health.timeUtc}
          </p>
        )}
        {problem !== null && (
          <p className="alert" role="alert" id="health-p-6">
            <CodeTag code={problem.code} tone="bad"  id="health-codetag-7"/> {problem.title}
            {problem.detail !== "" ? ` — ${problem.detail}` : ""}
          </p>
        )}
      </Module>

      <Module
        code="HLT-02"
        title="Latency"
        span="span-4"
        note="Rolling window of the last 60 probes."
       id="health-module-8">
        <Sparkline samples={samples} label="Health probe latency"  id="health-sparkline-9"/>
        <Legend
          label="Latency chart key"
          items={[
            { key: "hit", swatch: <Dot color="var(--sig)" />, text: "hit — probe answered" },
            { key: "miss", swatch: <Dot color="var(--red)"  id="health-dot-10"/>, text: "miss — probe failed" },
          ]}
        />
        <div className="stat-grid" style={{ marginTop: "10px" }} id="health-div-11">
          <Stat label="Probes" value={stats === null ? "0" : String(stats.count)}  id="health-stat-12"/>
          <Stat
            label="Last"
            value={stats === null ? "—" : formatMs(stats.last)}
            tone={stats ? "ok" : undefined}
           id="health-stat-13"/>
          <Stat label="p50" value={stats === null ? "—" : formatMs(stats.p50)}  id="health-stat-14"/>
          <Stat label="Min" value={stats === null ? "—" : formatMs(stats.min)}  id="health-stat-15"/>
          <Stat label="Max" value={stats === null ? "—" : formatMs(stats.max)}  id="health-stat-16"/>
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
          <div className="tbl-wrap" id="health-div-17">
            <table className="data" id="health-table-18">
              <thead id="health-thead-19">
                <tr id="health-tr-20">
                  <th id="health-th-21">#</th>
                  <th>Round trip</th>
                  <th>Outcome</th>
                </tr>
              </thead>
              <tbody>
                {samples.slice(-12).map((s, i) => (
                  <tr key={`${s.at}-${i}`} id="health-tr-22">
                    <td>{samples.length - Math.min(samples.length, 12) + i + 1}</td>
                    <td>{formatMs(s.ms)}</td>
                    <td id="health-td-23">
                      <CodeTag code={s.ok ? "OK" : "FAIL"} tone={s.ok ? "ok" : "bad"}  id="health-codetag-24"/>
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
        note={
          <>
            Exactly what the{" "}
            <Abbr term="API" full="Application Programming Interface: the .NET data plane"  id="health-abbr-25"/>{" "}
            returned — the deck renders fields, never prose.
          </>
        }
      >
        {health === null ? (
          <p className="status-line">Run a probe to capture the payload.</p>
        ) : (
          <JsonBlock label="Health payload" value={health}  id="health-jsonblock-26"/>
        )}
      </Module>

      <Module
        code="HLT-05"
        title="Reading the dials"
        span="span-6"
        note="What each field guarantees."
       id="health-module-27">
        <div className="tbl-wrap" id="health-div-28">
          <table className="data" id="health-table-29">
            <thead id="health-thead-30">
              <tr id="health-tr-31">
                <th>Signal</th>
                <th>Meaning</th>
              </tr>
            </thead>
            <tbody id="health-tbody-32">
              <tr id="health-tr-33">
                <td>status = healthy</td>
                <td id="health-td-34">
                  <Abbr term="API" full="Application Programming Interface: the .NET data plane"  id="health-abbr-35"/>{" "}
                  process alive, JSON pipeline intact, clock readable.
                </td>
              </tr>
              <tr id="health-tr-36">
                <td>timeUtc</td>
                <td>
                  Server clock at response time in{" "}
                  <Abbr term="UTC" full="Coordinated Universal Time"  id="health-abbr-37"/>; drift against the deck
                  clock hints at environment skew.
                </td>
              </tr>
              <tr id="health-tr-38">
                <td>round trip</td>
                <td>
                  Browser → <Abbr term="BFF" full="Backend for Frontend"  id="health-abbr-39"/> → API → back. Spikes
                  implicate the proxy path, not just the API.
                </td>
              </tr>
              <tr id="health-tr-40">
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
