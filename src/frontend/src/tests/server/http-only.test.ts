import { afterEach, describe, expect, test } from "bun:test";

import { parseConfig } from "../../server/config.ts";
import { createHandler } from "../../server/index.ts";

let servers: Array<{ stop(): void }> = [];

afterEach(() => {
  for (const server of servers) {
    server.stop();
  }
  servers = [];
});

/** Stand-in for the .NET http endpoint (`http://localhost:5207` in the real dev loop). */
function httpUpstream(): string {
  const server = Bun.serve({
    port: 0,
    hostname: "127.0.0.1",
    fetch: () => Response.json({ status: "healthy" }),
  });
  servers.push(server);
  return `http://127.0.0.1:${server.port}`;
}

async function packageScripts(): Promise<Record<string, string>> {
  const pkg = (await Bun.file(new URL("../../../package.json", import.meta.url)).json()) as {
    scripts: Record<string, string>;
  };
  return pkg.scripts;
}

describe("http-only dev lane", () => {
  test("parses the http-only env: http upstream, no TLS pair", () => {
    expect(parseConfig({ PORT: "3000", DOTNET_API_URL: "http://127.0.0.1:5207" })).toEqual({
      port: 3000,
      dotnetApiUrl: "http://127.0.0.1:5207",
      tlsCertFile: undefined,
      tlsKeyFile: undefined,
    });
  });

  test("serves plain HTTP and proxies /api/* to the http upstream", async () => {
    const upstream = httpUpstream();
    const config = parseConfig({ DOTNET_API_URL: upstream });
    const server = Bun.serve({
      port: 0,
      hostname: "127.0.0.1",
      fetch: createHandler(config).fetch,
    });
    servers.push(server);

    const base = `http://127.0.0.1:${server.port}`;
    const health = await fetch(`${base}/healthz`);
    expect(health.status).toBe(200);
    expect(await health.json()).toEqual({ status: "ok", upstream });

    const proxied = await fetch(`${base}/api/v1/health`);
    expect(proxied.status).toBe(200);
    expect(await proxied.json()).toEqual({ status: "healthy" });
  });

  test("ships add-on scripts that are cert-free; the https lane keeps its CA", async () => {
    const scripts = await packageScripts();

    const http = scripts["dev:http"];
    expect(http).toBeDefined();
    expect(http).not.toContain("NODE_EXTRA_CA_CERTS");
    expect(http).toContain("http://127.0.0.1:5207");
    expect(http).toContain("src/server/dev.ts");

    const httpTypes = scripts["api:types:http"];
    expect(httpTypes).toBeDefined();
    expect(httpTypes).not.toContain("NODE_EXTRA_CA_CERTS");
    expect(httpTypes).toContain("http://localhost:5207/openapi/v1.json");

    // The opt-in HTTPS lane is untouched: its CA and https URLs stay baked in.
    expect(scripts["dev:server"]).toContain("NODE_EXTRA_CA_CERTS=./.certs/devcert.pem");
    expect(scripts["api:types"]).toContain("https://localhost:5208/openapi/v1.json");
  });
});
