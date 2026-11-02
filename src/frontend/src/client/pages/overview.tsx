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
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // OVERVIEW" />
        </h1>
        <p>Engine state, battlespace, and killfeed at a glance. Drill into a tab for detail.</p>
      </div>

      <Module
        code="MOD-01"
        title="Engine state"
        span="span-4"
        note="Last GET /api/v1/health through the BFF proxy."
      >
        <div className="stat-grid">
          <Stat label="Engine" value={health?.status ?? "—"} tone={health ? "ok" : undefined} />
          <Stat
            label="Last round trip"
            value={healthMs === null ? "—" : formatMs(healthMs)}
            tone={healthMs === null ? undefined : "ok"}
          />
          <Stat
            label="API plane"
            value={apiState.toUpperCase()}
            tone={apiState === "live" ? "ok" : apiState === "down" ? "bad" : undefined}
          />
          <Stat label="Samples" value={stats === null ? "0" : String(stats.count)} />
        </div>
        {health !== null && (
          <div style={{ marginTop: "10px" }}>
            <JsonBlock label="Latest health payload" value={health} />
          </div>
        )}
      </Module>

      <Module
        code="MOD-02"
        title="Proxy latency"
        span="span-4"
        note="Round-trip ms per health probe, newest on the right."
      >
        <Sparkline samples={samples} label="Proxy latency" />
        <Legend
          label="Latency chart key"
          items={[
            { key: "hit", swatch: <Dot color="var(--sig)" />, text: "hit — probe answered" },
            { key: "miss", swatch: <Dot color="var(--red)" />, text: "miss — probe failed" },
            { key: "line", swatch: <Wire color="var(--sig)" />, text: "round-trip ms over time" },
          ]}
        />
        {stats !== null && (
          <div className="stat-grid" style={{ marginTop: "10px" }}>
            <Stat label="p50" value={formatMs(stats.p50)} />
            <Stat label="min" value={formatMs(stats.min)} />
            <Stat label="max" value={formatMs(stats.max)} />
          </div>
        )}
      </Module>

      <Module
        code="MOD-03"
        title="Tac-map // god's eye"
        span="span-4"
        note={
          <>
            <Abbr term="BFF" full="Backend for Frontend" /> is{" "}
            <Abbr term="HQ" full="Headquarters: the tac-map center" />. Tracers ride live wires; X
            marks failed probes.
          </>
        }
      >
        <TacMap samples={samples} apiState={apiState} />
        <Legend
          label="Tac-map key"
          items={[
            { key: "unit", swatch: <Diamond color="var(--sig)" />, text: "unit — color is state" },
            { key: "hq", swatch: <Ring color="var(--sig)" />, text: "HQ — the BFF" },
            { key: "wire", swatch: <Wire color="var(--sig)" />, text: "live wire" },
            {
              key: "unknown",
              swatch: <Wire color="var(--faint)" dashed />,
              text: "unknown wire",
            },
            { key: "blip", swatch: <Dot color="var(--sig)" />, text: "probe hit" },
            { key: "kill", swatch: <Cross color="var(--red)" />, text: "probe miss" },
          ]}
        />
      </Module>

      <Module
        code="MOD-04"
        title="Endpoint catalog"
        span="span-7"
        note={
          <>
            Every route the <Abbr term="BFF" full="Backend for Frontend" /> exposes or proxies, and
            the handler that owns it.
          </>
        }
      >
        <div className="tbl-wrap">
          <table className="data">
            <thead>
              <tr>
                <th>Method</th>
                <th>Path</th>
                <th>Owner</th>
                <th>Contract</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>GET</td>
                <td>/healthz</td>
                <td>BFF (index.ts)</td>
                <td>
                  <CodeTag code="200 · ok" tone="ok" />
                </td>
              </tr>
              <tr>
                <td>GET</td>
                <td>/api/v1/health</td>
                <td>GetHealthHandler</td>
                <td>
                  <CodeTag code="200 · HealthPayload" tone="ok" />
                </td>
              </tr>
              <tr>
                <td>GET</td>
                <td>/api/v1/search-profiles</td>
                <td>ListSearchProfilesHandler</td>
                <td>
                  <CodeTag code="200 · items+total" tone="ok" />
                </td>
              </tr>
              <tr>
                <td>POST</td>
                <td>/api/v1/search-profiles</td>
                <td>CreateSearchProfileHandler</td>
                <td>
                  <CodeTag code="201 / 400+errors / 409" tone="warn" />
                </td>
              </tr>
              <tr>
                <td>GET · POST</td>
                <td>/api/v1/sessions</td>
                <td>List/CreateSessionHandler</td>
                <td>
                  <CodeTag code="200 · 201 / 400 / 409" tone="warn" />
                </td>
              </tr>
              <tr>
                <td>GET</td>
                <td>/api/v1/sources</td>
                <td>ListSourcesHandler</td>
                <td>
                  <CodeTag code="200 · gate+quota+circuit" tone="ok" />
                </td>
              </tr>
              <tr>
                <td>GET</td>
                <td>/api/v1/search-profiles/{"{id}"}</td>
                <td>GetSearchProfileHandler</td>
                <td>
                  <CodeTag code="200 / 404+code / 400" tone="warn" />
                </td>
              </tr>
              <tr>
                <td>*</td>
                <td>/api/* (unmatched)</td>
                <td>BFF proxy → API 404</td>
                <td>
                  <CodeTag code="passthrough" />
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
              >
                <time>{e.at}</time>
                <CodeTag code={e.code} tone={codeTone(e.code)} />
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
            { key: "kill", swatch: <Cross color="var(--red)" />, text: "✕ kill — action failed" },
          ]}
        />
      </Module>
    </>
  );
}
