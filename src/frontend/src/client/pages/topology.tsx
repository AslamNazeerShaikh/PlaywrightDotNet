import { DiagramDefs, type DiagramTone, markerId, TONE } from "../components/diagram.tsx";
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
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // TOPOLOGY" />
        </h1>
        <p>Two processes, one same-origin contract. Browser never touches the API directly.</p>
      </div>

      <Module
        code="TOP-01"
        title="Request lifecycle"
        span="span-7"
        note="Three paths through the same proxy: hit, miss, plane down."
      >
        <svg
          className="diagram"
          viewBox="0 0 940 524"
          role="img"
          aria-label="Request lifecycle sequence"
        >
          <DiagramDefs prefix="top" />
          {nodeHeader(130, "BROWSER", "operator tab", "cyan")}
          {nodeHeader(370, "BFF :3000", "Bun.serve proxy", "green")}
          {nodeHeader(610, ".NET :5208", "ASP.NET controllers", "violet")}
          {nodeHeader(850, "SQLITE", "artifacts/data", "amber")}
          {lifeline(130)}
          {lifeline(370)}
          {lifeline(610)}
          {lifeline(850)}
          <text
            x={250}
            y={88}
            textAnchor="middle"
            style={{ fill: "var(--faint)", fontSize: "var(--text-fine)" }}
          >
            same-origin
          </text>
          <text
            x={490}
            y={88}
            textAnchor="middle"
            style={{ fill: "var(--faint)", fontSize: "var(--text-fine)" }}
          >
            loopback
          </text>
          <text
            x={730}
            y={88}
            textAnchor="middle"
            style={{ fill: "var(--faint)", fontSize: "var(--text-fine)" }}
          >
            Dapper
          </text>

          {laneBand(108, 190, "green", "HIT", "200")}
          {message(130, 370, 136, "green", "GET /api/v1/search-profiles/:id")}
          {message(370, 610, 166, "green", "forward /api/*")}
          {message(610, 850, 196, "green", "GetAsync(profileId)")}
          {message(850, 610, 226, "amber", "profile row", true)}
          {message(610, 370, 256, "green", "200 · profile JSON", true)}
          {message(370, 130, 286, "green", "stream back", true)}

          {laneBand(310, 86, "red", "MISS", "404")}
          {message(610, 370, 338, "red", "404 · PROFILE_NOT_FOUND")}
          {message(370, 130, 368, "red", "404 · problem+json", true)}

          {laneBand(408, 100, "amber", "DOWN", "502")}
          {selfLoop(370, 430, "amber", "connect refused", "ECONNREFUSED")}
          {message(370, 130, 482, "amber", "502 · DOTNET_UNREACHABLE", true)}
        </svg>
        <Legend
          label="Sequence key"
          items={[
            { key: "hit", swatch: <Wire color="var(--sig)" />, text: "happy path — 200" },
            { key: "miss", swatch: <Wire color="var(--red)" />, text: "miss path — 404 + code" },
            {
              key: "down",
              swatch: <Wire color="var(--amber)" />,
              text: "plane down — BFF answers 502",
            },
          ]}
        />
      </Module>

      <div className="module-stack">
        <Module
          code="TOP-02"
          title="Processes"
          span="span-5"
          note={
            <>
              Loopback-only. No <Abbr term="CORS" full="Cross-Origin Resource Sharing" />, no
              cross-origin calls, ever.
            </>
          }
        >
          <div className="tbl-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>Process</th>
                  <th>Binds</th>
                  <th>Owns</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>
                    <Abbr term="BFF" full="Backend for Frontend" /> (Bun.serve)
                  </td>
                  <td>127.0.0.1:3000</td>
                  <td>
                    /healthz, /api/* proxy, <Abbr term="SPA" full="Single-Page Application" />{" "}
                    static, <Abbr term="HMR" full="Hot Module Replacement" /> in dev
                  </td>
                </tr>
                <tr>
                  <td>
                    <Abbr
                      term="API"
                      full="Application Programming Interface: the .NET data plane"
                    />{" "}
                    (ASP.NET)
                  </td>
                  <td>localhost:5208</td>
                  <td>Controllers, handlers, in-memory store, RFC 9457 mapping</td>
                </tr>
                <tr>
                  <td>Upstream</td>
                  <td>{upstream}</td>
                  <td>DOTNET_API_URL; the single wire between processes</td>
                </tr>
              </tbody>
            </table>
          </div>
          <p className="status-line" style={{ marginTop: "var(--gap)" }}>
            Failure rule: the browser shows the BFF answer. A 502 means the BFF is fine and the API
            is not — restart the data plane, not the deck.
          </p>
        </Module>

        <Module
          code="TOP-03"
          title="Layer contract"
          span="span-5"
          note="Who may talk to whom, enforced by construction."
        >
          <div className="tbl-wrap">
            <table className="data">
              <thead>
                <tr>
                  <th>From</th>
                  <th>To</th>
                  <th>Via</th>
                  <th>Forbidden</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td>React pages</td>
                  <td>BFF</td>
                  <td>Same-origin fetch to /api/*</td>
                  <td>Direct API URLs, CORS workarounds</td>
                </tr>
                <tr>
                  <td>BFF proxy</td>
                  <td>.NET API</td>
                  <td>Loopback fetch, hop-by-hop headers stripped</td>
                  <td>Forwarding cookies or auth headers it does not own</td>
                </tr>
                <tr>
                  <td>API controller</td>
                  <td>Handler → store</td>
                  <td>Constructor-injected use-case handler</td>
                  <td>Mediator, CQRS buses, generic repositories</td>
                </tr>
                <tr>
                  <td>Failures</td>
                  <td>Browser</td>
                  <td>Result → RFC 9457 + stable code + traceId</td>
                  <td>Stack traces, prose-sniffing in the UI</td>
                </tr>
              </tbody>
            </table>
          </div>
        </Module>
      </div>

      <Module
        code="TOP-04"
        title="Deck glossary"
        span="span-12"
        note="Every abbreviation on the deck, expanded once. Hover any dotted term anywhere for its meaning."
      >
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
        />
      </Module>
    </>
  );
}

function nodeHeader(x: number, label: string, caption: string, tone: DiagramTone) {
  return (
    <g>
      <rect
        x={x - 75}
        y={18}
        width="150"
        height="40"
        rx="6"
        style={{ fill: TONE[tone].fill, stroke: TONE[tone].stroke }}
        strokeWidth="1.3"
      />
      <text
        x={x}
        y={42}
        textAnchor="middle"
        style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
        fontWeight="600"
      >
        {label}
      </text>
      <text
        x={x}
        y={72}
        textAnchor="middle"
        style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
      >
        {caption}
      </text>
    </g>
  );
}

function lifeline(x: number) {
  return (
    <line
      x1={x}
      y1={100}
      x2={x}
      y2={500}
      strokeDasharray="4 6"
      style={{ stroke: "var(--line-hi)" }}
      strokeWidth="1"
    />
  );
}

function laneBand(y: number, h: number, tone: DiagramTone, label: string, code: string) {
  return (
    <g>
      <rect x={12} y={y} width="916" height={h} rx="8" style={{ fill: TONE[tone].fill }} />
      <rect
        x={22}
        y={y + 10}
        width="86"
        height="44"
        rx="5"
        style={{ fill: "var(--panel)", stroke: TONE[tone].stroke }}
      />
      <text
        x={65}
        y={y + 27}
        textAnchor="middle"
        style={{ fill: TONE[tone].text, fontSize: "var(--text-micro)" }}
        fontWeight="700"
      >
        {label}
      </text>
      <text
        x={65}
        y={y + 41}
        textAnchor="middle"
        style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
      >
        {code}
      </text>
    </g>
  );
}

function message(
  from: number,
  to: number,
  y: number,
  tone: DiagramTone,
  label: string,
  dashed = false,
) {
  const rightward = to > from;
  const x1 = rightward ? from + 5 : from - 5;
  const x2 = rightward ? to - 11 : to + 11;
  return (
    <g>
      <line
        x1={x1}
        y1={y}
        x2={x2}
        y2={y}
        strokeDasharray={dashed ? "5 4" : undefined}
        style={{ stroke: TONE[tone].stroke }}
        strokeWidth="1.5"
        markerEnd={`url(#${markerId("top", tone)})`}
      />
      <text
        x={(from + to) / 2}
        y={y - 9}
        textAnchor="middle"
        style={{ fill: TONE[tone].text, fontSize: "var(--text-fine)" }}
      >
        {label}
      </text>
    </g>
  );
}

function selfLoop(x: number, y: number, tone: DiagramTone, line: string, code: string) {
  return (
    <g>
      <path
        d={`M${x + 5},${y} H${x + 38} Q${x + 46},${y} ${x + 46},${y + 8} V${y + 20} Q${x + 46},${y + 28} ${x + 38},${y + 28} H${x + 11}`}
        fill="none"
        style={{ stroke: TONE[tone].stroke }}
        strokeWidth="1.5"
        markerEnd={`url(#${markerId("top", tone)})`}
      />
      <text x={x + 58} y={y + 10} style={{ fill: TONE[tone].text, fontSize: "var(--text-fine)" }}>
        {line}
      </text>
      <text x={x + 58} y={y + 22} style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}>
        {code}
      </text>
    </g>
  );
}
