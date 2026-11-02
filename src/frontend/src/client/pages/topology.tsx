import { Abbr, Glossary, Legend, Wire } from "../components/legend.tsx";
import { Module } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import { useRenderLog } from "../lib/debug.ts";

/**
 * Topology page: how the cockpit is wired. A sequence diagram for the request lifecycle (happy +
 * miss + API-down paths), a component map of the two processes, and the layer contract. SVG scales
 * with the module; tables carry the dense detail.
 */
export function TopologyPage({ upstream }: { upstream: string }) {
  useRenderLog("TopologyPage");
  return (
    <>
      <div className="page-head" id="topology-div-1">
        <h1 id="topology-h1-2">
          <Scramble text="OPS-DECK // TOPOLOGY"  id="topology-scramble-3"/>
        </h1>
        <p>Two processes, one same-origin contract. Browser never touches the API directly.</p>
      </div>

      <Module
        code="TOP-01"
        title="Request lifecycle"
        span="span-7"
        note="Three paths through the same proxy: hit, miss, plane down."
       id="topology-module-4">
        <svg
          className="diagram"
          viewBox="0 0 640 300"
          role="img"
          aria-label="Request lifecycle sequence"
        >
          {lifeline(40, "BROWSER")}
          {lifeline(230, "BFF :3000")}
          {lifeline(420, ".NET :5208")}
          {lifeline(580, "STORE")}
          {arrow(40, 230, 46, "var(--sig)", "GET /api/v1/…")}
          {arrow(230, 420, 70, "var(--sig)", "proxy → loopback")}
          {arrow(420, 580, 94, "var(--sig)", "GetAsync(id)")}
          {arrow(580, 420, 118, "var(--sig)", "hit? profile : null")}
          {arrow(420, 230, 142, "var(--sig)", "200 · payload")}
          {arrow(230, 40, 166, "var(--sig)", "stream back")}
          {arrow(420, 230, 206, "var(--red)", "404 + PROFILE_NOT_FOUND")}
          {arrow(230, 230, 246, "var(--amber)", "API down → 502 DOTNET_UNREACHABLE", true)}
        </svg>
        <Legend
          label="Sequence key"
          items={[
            { key: "hit", swatch: <Wire color="var(--sig)" />, text: "happy path — 200" },
            { key: "miss", swatch: <Wire color="var(--red)"  id="topology-wire-5"/>, text: "miss path — 404 + code" },
            {
              key: "down",
              swatch: <Wire color="var(--amber)"  id="topology-wire-6"/>,
              text: "plane down — self-loop answers 502",
            },
          ]}
        />
      </Module>

      <Module
        code="TOP-02"
        title="Processes"
        span="span-5"
        note={
          <>
            Loopback-only. No <Abbr term="CORS" full="Cross-Origin Resource Sharing"  id="topology-abbr-7"/>, no
            cross-origin calls, ever.
          </>
        }
      >
        <div className="tbl-wrap" id="topology-div-8">
          <table className="data" id="topology-table-9">
            <thead id="topology-thead-10">
              <tr id="topology-tr-11">
                <th>Process</th>
                <th>Binds</th>
                <th>Owns</th>
              </tr>
            </thead>
            <tbody id="topology-tbody-12">
              <tr id="topology-tr-13">
                <td id="topology-td-14">
                  <Abbr term="BFF" full="Backend for Frontend"  id="topology-abbr-15"/> (Bun.serve)
                </td>
                <td>127.0.0.1:3000</td>
                <td id="topology-td-16">
                  /healthz, /api/* proxy, <Abbr term="SPA" full="Single-Page Application"  id="topology-abbr-17"/> static,{" "}
                  <Abbr term="HMR" full="Hot Module Replacement"  id="topology-abbr-18"/> in dev
                </td>
              </tr>
              <tr id="topology-tr-19">
                <td id="topology-td-20">
                  <Abbr term="API" full="Application Programming Interface: the .NET data plane"  id="topology-abbr-21"/>{" "}
                  (ASP.NET)
                </td>
                <td>localhost:5208</td>
                <td>Controllers, handlers, in-memory store, RFC 9457 mapping</td>
              </tr>
              <tr id="topology-tr-22">
                <td>Upstream</td>
                <td>{upstream}</td>
                <td>DOTNET_API_URL; the single wire between processes</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p className="status-line" style={{ marginTop: "10px" }}>
          Failure rule: the browser shows the BFF answer. A 502 means the BFF is fine and the API is
          not — restart the data plane, not the deck.
        </p>
      </Module>

      <Module
        code="TOP-03"
        title="Layer contract"
        span="span-12"
        note="Who may talk to whom, enforced by construction."
       id="topology-module-23">
        <div className="tbl-wrap" id="topology-div-24">
          <table className="data" id="topology-table-25">
            <thead id="topology-thead-26">
              <tr id="topology-tr-27">
                <th>From</th>
                <th>To</th>
                <th>Via</th>
                <th>Forbidden</th>
              </tr>
            </thead>
            <tbody id="topology-tbody-28">
              <tr id="topology-tr-29">
                <td>React pages</td>
                <td>BFF</td>
                <td>Same-origin fetch to /api/*</td>
                <td>Direct API URLs, CORS workarounds</td>
              </tr>
              <tr id="topology-tr-30">
                <td>BFF proxy</td>
                <td id="topology-td-31">.NET API</td>
                <td>Loopback fetch, hop-by-hop headers stripped</td>
                <td>Forwarding cookies or auth headers it does not own</td>
              </tr>
              <tr id="topology-tr-32">
                <td>API controller</td>
                <td>Handler → store</td>
                <td>Constructor-injected use-case handler</td>
                <td>Mediator, CQRS buses, generic repositories</td>
              </tr>
              <tr id="topology-tr-33">
                <td>Failures</td>
                <td>Browser</td>
                <td>Result → RFC 9457 + stable code + traceId</td>
                <td>Stack traces, prose-sniffing in the UI</td>
              </tr>
            </tbody>
          </table>
        </div>
      </Module>

      <Module
        code="TOP-04"
        title="Deck glossary"
        span="span-12"
        note="Every abbreviation on the deck, expanded once. Hover any dotted term anywhere for its meaning."
       id="topology-module-34">
        <Glossary
          terms={[
            {
              abbr: "BFF",
              full: "Backend for Frontend",
              hint: "The Bun process on :3000. Serves the deck, proxies /api/* loopback-only, owns no business logic.",
            },
            {
              abbr: "API",
              full: "Application Programming Interface",
              hint: "The .NET data plane on :5208 (https; :5207 http fallback). Controllers, handlers, and the store live here.",
            },
            {
              abbr: "HQ",
              full: "Headquarters",
              hint: "The tac-map center: the BFF. All wires run through it.",
            },
            {
              abbr: "MOD / HLT / PRF / TOP",
              full: "Module code prefixes",
              hint: "Which page owns the card: overview, health, profiles, topology.",
            },
            {
              abbr: "UTC",
              full: "Coordinated Universal Time",
              hint: "Every timestamp on the deck. Compare against the server timeUtc to spot clock skew.",
            },
            {
              abbr: "P50",
              full: "50th percentile",
              hint: "Median round-trip: half the probes were faster, half slower. Robust against one-off spikes.",
            },
            {
              abbr: "HMR",
              full: "Hot Module Replacement",
              hint: "The dev server swaps edited modules live; the deck never needs a refresh while building.",
            },
            {
              abbr: "SPA",
              full: "Single-Page Application",
              hint: "The React bundle served at /. Hash routes switch pages without a server round trip.",
            },
            {
              abbr: "SSE",
              full: "Server-Sent Events",
              hint: "Planned: the runs feed will stream over SSE through the BFF proxy. Not wired yet.",
            },
            {
              abbr: "CORS",
              full: "Cross-Origin Resource Sharing",
              hint: "Never opened. The browser talks same-origin to the BFF; the BFF talks loopback to the API.",
            },
          ]}
         id="topology-glossary-35"/>
      </Module>
    </>
  );
}

function lifeline(x: number, label: string) {
  return (
    <g key={label} id="topology-g-36">
      <rect
        x={x - 58}
        y={8}
        width="116"
        height="26"
        rx="3"
        style={{ fill: "var(--canvas)", stroke: "var(--line-hi)" }}
       id="topology-rect-37"/>
      <text
        x={x}
        y={25}
        textAnchor="middle"
        style={{ fill: "var(--ink)", fontSize: "var(--text-label)" }}
        fontWeight="600"
      >
        {label}
      </text>
      <line
        x1={x}
        y1={38}
        x2={x}
        y2={292}
        style={{ stroke: "var(--line)" }}
        strokeDasharray="4 4"
       id="topology-line-38"/>
    </g>
  );
}

function arrow(x1: number, x2: number, y: number, color: string, label: string, self = false) {
  if (self) {
    return (
      <g key={`${label}-${y}`} id="topology-g-39">
        <path
          d={`M ${x1} ${y} h 34 v 14 h -34`}
          fill="none"
          style={{ stroke: color }}
          strokeWidth="1.5"
         id="topology-path-40"/>
        <text x={x1 + 40} y={y + 5} style={{ fill: "var(--dim)", fontSize: "var(--text-micro)" }}>
          {label}
        </text>
      </g>
    );
  }
  const left = x1 < x2;
  return (
    <g key={`${label}-${y}`} id="topology-g-41">
      <line x1={x1} y1={y} x2={x2} y2={y} style={{ stroke: color }} strokeWidth="1.5"  id="topology-line-42"/>
      <polygon
        points={
          left
            ? `${x2},${y} ${x2 - 7},${y - 3.5} ${x2 - 7},${y + 3.5}`
            : `${x2},${y} ${x2 + 7},${y - 3.5} ${x2 + 7},${y + 3.5}`
        }
        style={{ fill: color }}
       id="topology-polygon-43"/>
      <text
        x={left ? x1 + 6 : x2 + 6}
        y={y - 5}
        style={{ fill: "var(--dim)", fontSize: "var(--text-micro)" }}
      >
        {label}
      </text>
    </g>
  );
}
