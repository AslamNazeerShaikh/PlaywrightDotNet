import { useMemo } from "react";

import { FlowCanvas } from "../components/FlowCanvas.tsx";
import { Abbr, Glossary, Legend, Wire } from "../components/legend.tsx";
import { Module } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import { useRenderLog } from "../lib/debug.ts";
import { buildLifecycleNodesEdges } from "../lib/flow-graphs.ts";

/**
 * Topology page: how the cockpit is wired. A sequence diagram for the request lifecycle (happy +
 * miss + API-down paths), a component map of the two processes, and the layer contract. SVG scales
 * with the module; tables carry the dense detail.
 */
export function TopologyPage({ upstream }: { upstream: string }) {
  useRenderLog("TopologyPage");
  const lifecycle = useMemo(buildLifecycleNodesEdges, []);
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
        <FlowCanvas
          nodes={lifecycle.nodes}
          edges={lifecycle.edges}
          label="Request lifecycle sequence"
          testId="lifecycle-flow"
        />
        <Legend
          label="Sequence key"
          items={[
            { key: "hit", swatch: <Wire color="var(--sig)" />, text: "happy path — 200" },
            { key: "miss", swatch: <Wire color="var(--red)" />, text: "miss path — 404 + code" },
            {
              key: "down",
              swatch: <Wire color="var(--amber)" />,
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
                  /healthz, /api/* proxy, <Abbr term="SPA" full="Single-Page Application" /> static,{" "}
                  <Abbr term="HMR" full="Hot Module Replacement" /> in dev
                </td>
              </tr>
              <tr>
                <td>
                  <Abbr term="API" full="Application Programming Interface: the .NET data plane" />{" "}
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
