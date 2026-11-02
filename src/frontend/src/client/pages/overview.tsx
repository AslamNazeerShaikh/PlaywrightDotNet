import { Abbr, Cross, Diamond, Dot, Legend, Ring, Wire } from "../components/legend.tsx";
import { CodeTag, codeTone, JsonBlock, Module, Stat } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import { Sparkline } from "../components/sparkline.tsx";
import { TacMap } from "../components/tacmap.tsx";
import type { HealthPayload } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import { formatMs, type LatencyStats, type LogEvent, type Sample } from "../lib/telemetry.ts";

/**
 * Central dashboard: engine state, proxy latency chart, live request path, endpoint catalog, and
 * the ops event log. Every module is a grid cell that reflows at runtime; nothing here assumes a
 * minimum viewport width.
 */
export function OverviewPage({
  health,
  healthMs,
  stats,
  samples,
  events,
  apiState,
}: {
  health: HealthPayload | null;
  healthMs: number | null;
  stats: LatencyStats | null;
  samples: Sample[];
  events: LogEvent[];
  apiState: "unknown" | "live" | "down";
}) {
  useRenderLog("OverviewPage");
  return (
    <>
      <div className="page-head" id="overview-div-1">
        <h1 id="overview-h1-2">
          <Scramble text="OPS-DECK // OVERVIEW"  id="overview-scramble-3"/>
        </h1>
        <p>Engine state, battlespace, and killfeed at a glance. Drill into a tab for detail.</p>
      </div>

      <Module
        code="MOD-01"
        title="Engine state"
        span="span-4"
        note="Last GET /api/v1/health through the BFF proxy."
       id="overview-module-4">
        <div className="stat-grid" id="overview-div-5">
          <Stat label="Engine" value={health?.status ?? "—"} tone={health ? "ok" : undefined}  id="overview-stat-6"/>
          <Stat
            label="Last round trip"
            value={healthMs === null ? "—" : formatMs(healthMs)}
            tone={healthMs === null ? undefined : "ok"}
           id="overview-stat-7"/>
          <Stat
            label="API plane"
            value={apiState.toUpperCase()}
            tone={apiState === "live" ? "ok" : apiState === "down" ? "bad" : undefined}
           id="overview-stat-8"/>
          <Stat label="Samples" value={stats === null ? "0" : String(stats.count)}  id="overview-stat-9"/>
        </div>
        {health !== null && (
          <div style={{ marginTop: "10px" }} id="overview-div-10">
            <JsonBlock label="Latest health payload" value={health}  id="overview-jsonblock-11"/>
          </div>
        )}
      </Module>

      <Module
        code="MOD-02"
        title="Proxy latency"
        span="span-4"
        note="Round-trip ms per health probe, newest on the right."
       id="overview-module-12">
        <Sparkline samples={samples} label="Proxy latency"  id="overview-sparkline-13"/>
        <Legend
          label="Latency chart key"
          items={[
            { key: "hit", swatch: <Dot color="var(--sig)" />, text: "hit — probe answered" },
            { key: "miss", swatch: <Dot color="var(--red)"  id="overview-dot-14"/>, text: "miss — probe failed" },
            { key: "line", swatch: <Wire color="var(--sig)"  id="overview-wire-15"/>, text: "round-trip ms over time" },
          ]}
        />
        {stats !== null && (
          <div className="stat-grid" style={{ marginTop: "10px" }} id="overview-div-16">
            <Stat label="p50" value={formatMs(stats.p50)}  id="overview-stat-17"/>
            <Stat label="min" value={formatMs(stats.min)}  id="overview-stat-18"/>
            <Stat label="max" value={formatMs(stats.max)}  id="overview-stat-19"/>
          </div>
        )}
      </Module>

      <Module
        code="MOD-03"
        title="Tac-map // god's eye"
        span="span-4"
        note={
          <>
            <Abbr term="BFF" full="Backend for Frontend"  id="overview-abbr-20"/> is{" "}
            <Abbr term="HQ" full="Headquarters: the tac-map center"  id="overview-abbr-21"/>. Tracers ride live wires; X
            marks failed probes.
          </>
        }
      >
        <TacMap samples={samples} apiState={apiState}  id="overview-tacmap-22"/>
        <Legend
          label="Tac-map key"
          items={[
            { key: "unit", swatch: <Diamond color="var(--sig)" />, text: "unit — color is state" },
            { key: "hq", swatch: <Ring color="var(--sig)"  id="overview-ring-23"/>, text: "HQ — the BFF" },
            { key: "wire", swatch: <Wire color="var(--sig)"  id="overview-wire-24"/>, text: "live wire" },
            {
              key: "unknown",
              swatch: <Wire color="var(--faint)" dashed  id="overview-wire-25"/>,
              text: "unknown wire",
            },
            { key: "blip", swatch: <Dot color="var(--sig)"  id="overview-dot-26"/>, text: "probe hit" },
            { key: "kill", swatch: <Cross color="var(--red)"  id="overview-cross-27"/>, text: "probe miss" },
          ]}
        />
      </Module>

      <Module
        code="MOD-04"
        title="Endpoint catalog"
        span="span-7"
        note={
          <>
            Every route the <Abbr term="BFF" full="Backend for Frontend"  id="overview-abbr-28"/> exposes or proxies, and
            the handler that owns it.
          </>
        }
      >
        <div className="tbl-wrap" id="overview-div-29">
          <table className="data" id="overview-table-30">
            <thead id="overview-thead-31">
              <tr id="overview-tr-32">
                <th>Method</th>
                <th>Path</th>
                <th>Owner</th>
                <th>Contract</th>
              </tr>
            </thead>
            <tbody id="overview-tbody-33">
              <tr id="overview-tr-34">
                <td>GET</td>
                <td id="overview-td-35">/healthz</td>
                <td>BFF (index.ts)</td>
                <td id="overview-td-36">
                  <CodeTag code="200 · ok" tone="ok"  id="overview-codetag-37"/>
                </td>
              </tr>
              <tr id="overview-tr-38">
                <td>GET</td>
                <td id="overview-td-39">/api/v1/health</td>
                <td>GetHealthHandler</td>
                <td id="overview-td-40">
                  <CodeTag code="200 · HealthPayload" tone="ok"  id="overview-codetag-41"/>
                </td>
              </tr>
              <tr id="overview-tr-42">
                <td>GET</td>
                <td id="overview-td-43">/api/v1/search-profiles</td>
                <td>ListSearchProfilesHandler</td>
                <td id="overview-td-44">
                  <CodeTag code="200 · items+total" tone="ok"  id="overview-codetag-45"/>
                </td>
              </tr>
              <tr id="overview-tr-46">
                <td>POST</td>
                <td id="overview-td-47">/api/v1/search-profiles</td>
                <td>CreateSearchProfileHandler</td>
                <td id="overview-td-48">
                  <CodeTag code="201 / 400+errors / 409" tone="warn"  id="overview-codetag-49"/>
                </td>
              </tr>
              <tr id="overview-tr-50">
                <td>GET · POST</td>
                <td id="overview-td-51">/api/v1/sessions</td>
                <td>List/CreateSessionHandler</td>
                <td id="overview-td-52">
                  <CodeTag code="200 · 201 / 400 / 409" tone="warn"  id="overview-codetag-53"/>
                </td>
              </tr>
              <tr id="overview-tr-54">
                <td>GET</td>
                <td id="overview-td-55">/api/v1/sources</td>
                <td>ListSourcesHandler</td>
                <td id="overview-td-56">
                  <CodeTag code="200 · gate+quota+circuit" tone="ok"  id="overview-codetag-57"/>
                </td>
              </tr>
              <tr id="overview-tr-58">
                <td>GET</td>
                <td id="overview-td-59">/api/v1/search-profiles/{"{id}"}</td>
                <td>GetSearchProfileHandler</td>
                <td id="overview-td-60">
                  <CodeTag code="200 / 404+code / 400" tone="warn"  id="overview-codetag-61"/>
                </td>
              </tr>
              <tr id="overview-tr-62">
                <td id="overview-td-63">*</td>
                <td id="overview-td-64">/api/* (unmatched)</td>
                <td>BFF proxy → API 404</td>
                <td id="overview-td-65">
                  <CodeTag code="passthrough"  id="overview-codetag-66"/>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </Module>

      <Module
        code="MOD-05"
        title="Killfeed"
        span="span-5"
        note="Newest first. Every probe, lookup, and failure lands here with its stable code."
      >
        {events.length === 0 ? (
          <p className="status-line">No events yet — run a health check or a profile lookup.</p>
        ) : (
          <ul className="log feed">
            {events.map((e, i) => (
              <li
                key={`${e.at}-${i}`}
                className={
                  e.kind === "err" ? "feed-kill" : e.kind === "ok" ? "feed-ok" : "feed-info"
                }
               id="overview-li-67">
                <time>{e.at}</time>
                <CodeTag code={e.code} tone={codeTone(e.code)}  id="overview-codetag-68"/>
                <span>{e.detail}</span>
              </li>
            ))}
          </ul>
        )}
        <Legend
          label="Killfeed key"
          items={[
            {
              key: "confirm",
              swatch: <Dot color="var(--sig)" />,
              text: "▸ confirm — action landed",
            },
            { key: "kill", swatch: <Cross color="var(--red)"  id="overview-cross-69"/>, text: "✕ kill — action failed" },
          ]}
        />
      </Module>
    </>
  );
}
