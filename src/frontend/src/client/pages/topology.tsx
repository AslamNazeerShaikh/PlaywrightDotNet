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
        code="TOP-05"
        title="Current scrape flow"
        span="span-7"
        note="One pass over the pages; each card is added or refreshed."
      >
        <svg className="diagram" viewBox="0 0 720 612" role="img" aria-label="Current scrape flow">
          <DiagramDefs prefix="top5" />
          {flowRect("top5", 280, 14, 220, 46, "green", "SearchAsync", "search loop · page N", true)}
          {flowLine("top5", 280, 60, 280, 82, "green")}
          {flowDiamond(
            280,
            132,
            200,
            46,
            "green",
            "Per-card gates",
            "stale · skill mismatch",
            "foreign source · invalid",
          )}
          {flowLabel(491, 122, "drop", "gray", "middle")}
          {flowLine("top5", 480, 132, 500, 132, "gray")}
          {flowRect("top5", 601, 109, 198, 46, "gray", "Dropped", "LogCardDropped → skip")}
          {flowLabel(290, 192, "pass", "green")}
          {flowLine("top5", 280, 178, 280, 198, "green")}
          {flowRect("top5", 280, 198, 230, 52, "green", "UpsertAsync", "FindAsync → Add / Refresh")}
          {flowLine("top5", 280, 250, 280, 274, "green", false)}
          {flowLine("top5", 140, 274, 420, 274, "green", false)}
          {flowLabel(148, 290, "new", "green")}
          {flowLabel(428, 290, "known", "green")}
          {flowLine("top5", 140, 274, 140, 296, "green")}
          {flowLine("top5", 420, 274, 420, 296, "green")}
          {flowRect("top5", 140, 296, 210, 52, "green", "AddAsync", "new row · PostingsNew++")}
          {flowRect("top5", 420, 296, 210, 52, "green", "Refresh", "known row · refresh fields")}
          {flowLine("top5", 140, 348, 140, 374, "green", false)}
          {flowLine("top5", 420, 348, 420, 374, "green", false)}
          {flowLine("top5", 140, 374, 420, 374, "green", false)}
          {flowDot(280, 374, "green")}
          {flowLine("top5", 280, 374, 280, 396, "green")}
          {flowRect("top5", 280, 396, 230, 52, "green", "RunStats", "PostingsFound · PostingsNew")}
          {flowLine("top5", 280, 448, 280, 470, "green")}
          {flowRect(
            "top5",
            280,
            470,
            260,
            52,
            "green",
            "PendingEnrichment",
            "no Description · Take(MaxDetails)",
          )}
          {flowLine("top5", 280, 522, 280, 544, "green")}
          {flowRect(
            "top5",
            280,
            544,
            230,
            52,
            "green",
            "PostingsAsync",
            "LastSeenAt in run window",
          )}
        </svg>
        <Legend
          label="Flow key"
          items={[
            {
              key: "focal",
              swatch: <Wire color="var(--sig)" />,
              text: "focal path — happy-path write",
            },
            {
              key: "drop",
              swatch: <Wire color="var(--line-hi)" />,
              text: "dropped card — a gate rejected it",
            },
          ]}
        />
      </Module>

      <Module
        code="TOP-06"
        title="New-only run delta"
        span="span-5"
        note="Three divergences: Δ1 job budget from PostingsNew, Δ2 per-page budget counts added rows, Δ3 listing and export filter to FirstSeenAt >= run start; enrichment keeps running, fresh postings first."
      >
        <svg className="diagram" viewBox="0 0 480 600" role="img" aria-label="New-only run delta">
          <DiagramDefs prefix="top6" />
          {flowRect(
            "top6",
            175,
            14,
            250,
            52,
            "green",
            "SearchAsync",
            "Δ1 · budget from PostingsNew",
            true,
          )}
          {flowLine("top6", 175, 66, 175, 88, "green")}
          {flowDiamond(175, 132, 110, 44, "green", "Per-card gates", "unchanged")}
          {flowLabel(298, 122, "drop", "gray", "middle")}
          {flowLine("top6", 285, 132, 310, 132, "gray")}
          {flowRect("top6", 391, 109, 146, 46, "gray", "Dropped", "LogCardDropped")}
          {flowLabel(183, 188, "pass", "green")}
          {flowLine("top6", 175, 176, 175, 198, "green")}
          {flowRect(
            "top6",
            175,
            198,
            250,
            52,
            "green",
            "UpsertAsync",
            "Δ2 · counted = OnlyNew ? added : found",
          )}
          {flowLine("top6", 175, 250, 175, 272, "green", false)}
          {flowLine("top6", 100, 272, 360, 272, "green", false)}
          {flowLabel(108, 288, "new", "green")}
          {flowLabel(368, 288, "known", "green")}
          {flowLine("top6", 100, 272, 100, 294, "green")}
          {flowLine("top6", 360, 272, 360, 294, "green")}
          {flowRect("top6", 100, 294, 190, 52, "green", "AddAsync", "new row · PostingsNew++")}
          {flowRect("top6", 360, 294, 190, 52, "green", "Refresh", "known row · refresh fields")}
          {flowLine("top6", 100, 346, 100, 372, "green", false)}
          {flowLine("top6", 360, 346, 360, 372, "green", false)}
          {flowLine("top6", 100, 372, 360, 372, "green", false)}
          {flowDot(230, 372, "green")}
          {flowLine("top6", 230, 372, 230, 394, "green")}
          {flowRect("top6", 230, 394, 230, 52, "green", "RunStats", "PostingsFound · PostingsNew")}
          {flowLine("top6", 230, 446, 230, 464, "green")}
          {flowRect("top6", 230, 464, 250, 52, "green", "Enrich", "fresh first")}
          {flowLine("top6", 230, 516, 230, 532, "green")}
          {flowRect(
            "top6",
            230,
            532,
            260,
            52,
            "green",
            "PostingsAsync",
            "Δ3 · FirstSeenAt >= run start",
          )}
        </svg>
        <Legend
          label="Delta key"
          items={[
            {
              key: "focal",
              swatch: <Wire color="var(--sig)" />,
              text: "focal path — new-only rules (Δ1–Δ3)",
            },
            {
              key: "drop",
              swatch: <Wire color="var(--line-hi)" />,
              text: "dropped card — a gate rejected it",
            },
          ]}
        />
      </Module>

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

function flowRect(
  prefix: string,
  cx: number,
  y: number,
  w: number,
  h: number,
  tone: DiagramTone,
  label: string,
  caption: string,
  pill = false,
) {
  return (
    <g>
      <rect
        x={cx - w / 2}
        y={y}
        width={w}
        height={h}
        rx={pill ? h / 2 : 6}
        style={{ fill: TONE[tone].fill, stroke: TONE[tone].stroke }}
        strokeWidth="1.5"
        data-tone={tone}
        data-path={tone === "gray" ? "drop" : "focal"}
      />
      <text
        x={cx}
        y={y + 22}
        textAnchor="middle"
        style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
        fontWeight="600"
      >
        {label}
      </text>
      <text
        x={cx}
        y={y + 40}
        textAnchor="middle"
        style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
      >
        {caption}
      </text>
    </g>
  );
}

function flowDiamond(
  cx: number,
  cy: number,
  halfWidth: number,
  halfHeight: number,
  tone: DiagramTone,
  label: string,
  caption: string,
  caption2?: string,
) {
  const points = `${cx - halfWidth},${cy} ${cx},${cy - halfHeight} ${cx + halfWidth},${cy} ${cx},${cy + halfHeight}`;
  return (
    <g>
      <polygon
        points={points}
        strokeLinejoin="round"
        style={{ fill: TONE[tone].fill, stroke: TONE[tone].stroke }}
        strokeWidth="1.5"
        data-tone={tone}
        data-path={tone === "gray" ? "drop" : "focal"}
      />
      <text
        x={cx}
        y={caption2 === undefined ? cy - 2 : cy - 12}
        textAnchor="middle"
        style={{ fill: "var(--ink)", fontSize: "var(--text-micro)" }}
        fontWeight="600"
      >
        {label}
      </text>
      <text
        x={cx}
        y={caption2 === undefined ? cy + 15 : cy + 6}
        textAnchor="middle"
        style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
      >
        {caption}
      </text>
      {caption2 !== undefined && (
        <text
          x={cx}
          y={cy + 20}
          textAnchor="middle"
          style={{ fill: "var(--dim)", fontSize: "var(--text-fine)" }}
        >
          {caption2}
        </text>
      )}
    </g>
  );
}

function flowLine(
  prefix: string,
  x1: number,
  y1: number,
  x2: number,
  y2: number,
  tone: DiagramTone,
  withMarker = true,
) {
  return (
    <line
      x1={x1}
      y1={y1}
      x2={x2}
      y2={y2}
      strokeWidth="1.5"
      style={{ stroke: TONE[tone].stroke }}
      markerEnd={withMarker ? `url(#${markerId(prefix, tone)})` : undefined}
      data-tone={tone}
      data-path={tone === "gray" ? "drop" : "focal"}
    />
  );
}

function flowDot(cx: number, cy: number, tone: DiagramTone) {
  return (
    <circle
      cx={cx}
      cy={cy}
      r="3.5"
      style={{ fill: TONE[tone].stroke }}
      data-tone={tone}
      data-path={tone === "gray" ? "drop" : "focal"}
    />
  );
}

function flowLabel(
  x: number,
  y: number,
  text: string,
  tone: DiagramTone,
  anchor: "start" | "middle" | "end" = "start",
) {
  return (
    <text
      x={x}
      y={y}
      textAnchor={anchor}
      style={{ fill: TONE[tone].text, fontSize: "var(--text-fine)" }}
    >
      {text}
    </text>
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
