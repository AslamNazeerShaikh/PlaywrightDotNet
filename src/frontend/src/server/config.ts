/**
 * Cockpit BFF configuration. Parsing is pure (takes an env record) so it is unit-testable without
 * touching `Bun.env`; `index.ts` passes the real env. Only public, build-time-safe values live here
 * — never secrets.
 */
export interface CockpitConfig {
  /** Loopback port the BFF listens on. */
  port: number;
  /** Base URL of the .NET data plane, without a trailing slash. */
  dotnetApiUrl: string;
  /** PEM cert file for serving the deck over HTTPS. Undefined = plain HTTP. */
  tlsCertFile?: string;
  /** PEM key file for serving the deck over HTTPS. Undefined = plain HTTP. */
  tlsKeyFile?: string;
}

const DEFAULT_PORT = 3000;
/** Matches this repo: `JobScrape.Api` serves HTTPS on 5208 in Development. */
const DEFAULT_DOTNET_API_URL = "https://localhost:5208";

export function parseConfig(env: Record<string, string | undefined>): CockpitConfig {
  const port = parsePort(env["PORT"]);
  const dotnetApiUrl = parseApiUrl(env["DOTNET_API_URL"]);
  const tlsCertFile = parseOptionalPath(env["BFF_TLS_CERT_FILE"]);
  const tlsKeyFile = parseOptionalPath(env["BFF_TLS_KEY_FILE"]);
  if ((tlsCertFile === undefined) !== (tlsKeyFile === undefined)) {
    throw new Error(
      "Invalid TLS config: BFF_TLS_CERT_FILE and BFF_TLS_KEY_FILE must be set together.",
    );
  }
  return { port, dotnetApiUrl, tlsCertFile, tlsKeyFile };
}

function parsePort(raw: string | undefined): number {
  if (raw === undefined || raw === "") {
    return DEFAULT_PORT;
  }
  const port = Number(raw);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`Invalid PORT: ${JSON.stringify(raw)}. Expected an integer 1-65535.`);
  }
  return port;
}

function parseApiUrl(raw: string | undefined): string {
  const candidate = raw === undefined || raw === "" ? DEFAULT_DOTNET_API_URL : raw;
  let url: URL;
  try {
    url = new URL(candidate);
  } catch {
    throw new Error(`Invalid DOTNET_API_URL: ${JSON.stringify(raw)}. Expected an absolute URL.`);
  }
  if (url.protocol !== "http:" && url.protocol !== "https:") {
    throw new Error(`Invalid DOTNET_API_URL: ${JSON.stringify(raw)}. Only http(s) is supported.`);
  }
  return url.toString().replace(/\/+$/, "");
}

function parseOptionalPath(raw: string | undefined): string | undefined {
  if (raw === undefined || raw === "") {
    return undefined;
  }
  return raw;
}
