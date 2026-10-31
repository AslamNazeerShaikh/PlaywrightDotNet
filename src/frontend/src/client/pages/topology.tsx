import { Module } from "../components/modules.tsx";

/**
 * Topology page: how the cockpit is wired. A sequence diagram for the request lifecycle (happy +
 * miss + API-down paths), a component map of the two processes, and the layer contract. SVG scales
 * with the module; tables carry the dense detail.
 */
export function TopologyPage({ upstream }: { upstream: string }) {
  return (
    <>
      <div className="page-head">
        <h1>OPS-DECK // TOPOLOGY</h1>
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
          viewBox="0 0 640 300"
          role="img"
          aria-label="Request lifecycle sequence"
        >
          {lifeline(40, "BROWSER")}
          {lifeline(230, "BFF :3000")}
          {lifeline(420, ".NET :5207")}
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
      </Module>

      <Module
        code="TOP-02"
        title="Processes"
        span="span-5"
        note="Loopback-only. No CORS, no cross-origin calls, ever."
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
                <td>BFF (Bun.serve)</td>
                <td>127.0.0.1:3000</td>
                <td>/healthz, /api/* proxy, SPA static, HMR in dev</td>
              </tr>
              <tr>
                <td>API (ASP.NET)</td>
                <td>127.0.0.1:5207</td>
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
    </>
  );
}

function lifeline(x: number, label: string) {
  return (
    <g key={label}>
      <rect
        x={x - 58}
        y={8}
        width="116"
        height="26"
        rx="3"
        style={{ fill: "var(--canvas)", stroke: "var(--line-hi)" }}
      />
      <text
        x={x}
        y={25}
        textAnchor="middle"
        style={{ fill: "var(--ink)" }}
        fontSize="11"
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
      />
    </g>
  );
}

function arrow(x1: number, x2: number, y: number, color: string, label: string, self = false) {
  if (self) {
    return (
      <g key={`${label}-${y}`}>
        <path
          d={`M ${x1} ${y} h 34 v 14 h -34`}
          fill="none"
          style={{ stroke: color }}
          strokeWidth="1.5"
        />
        <text x={x1 + 40} y={y + 5} style={{ fill: "var(--dim)" }} fontSize="10">
          {label}
        </text>
      </g>
    );
  }
  const left = x1 < x2;
  return (
    <g key={`${label}-${y}`}>
      <line x1={x1} y1={y} x2={x2} y2={y} style={{ stroke: color }} strokeWidth="1.5" />
      <polygon
        points={
          left
            ? `${x2},${y} ${x2 - 7},${y - 3.5} ${x2 - 7},${y + 3.5}`
            : `${x2},${y} ${x2 + 7},${y - 3.5} ${x2 + 7},${y + 3.5}`
        }
        style={{ fill: color }}
      />
      <text x={left ? x1 + 6 : x2 + 6} y={y - 5} style={{ fill: "var(--dim)" }} fontSize="10">
        {label}
      </text>
    </g>
  );
}
