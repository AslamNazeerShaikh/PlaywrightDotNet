/**
 * Session cookie contract shared by the BFF: reading the `Cookie` header, resolving the session
 * token, and serializing the set-cookie strings the browser stores. Bun 1.4 ships no cookie helper,
 * so this module is the single source for both directions.
 */

/**
 * Session cookie names in read order. The `__Host-` form wins when TLS is on; the plain name is the
 * HTTP dev fallback.
 */
export const SESSION_COOKIE_NAMES = ["__Host-jobscrape_session", "jobscrape_session"] as const;

/** Attributes every BFF-issued cookie carries. */
export interface CookieOptions {
  /** Adds `Secure`; `__Host-` names always get it (the prefix requires it). */
  secure: boolean;
  /** Adds `HttpOnly`; CSRF cookies stay JS-readable and pass false. */
  httpOnly: boolean;
  /** Adds `Max-Age` when set. Omitted cookies live for the browser session. */
  maxAgeSeconds?: number;
}

/** Parses a `Cookie` header into a name->value map; malformed pairs are skipped. */
export function parseCookies(header: string | null): Record<string, string> {
  const out: Record<string, string> = {};
  if (header === null || header === "") {
    return out;
  }
  for (const part of header.split(";")) {
    const idx = part.indexOf("=");
    if (idx < 0) {
      continue;
    }
    const name = part.slice(0, idx).trim();
    const value = part.slice(idx + 1).trim();
    if (name !== "") {
      try {
        out[name] = decodeURIComponent(value);
      } catch {
        out[name] = value;
      }
    }
  }
  return out;
}

/** Reads the first non-empty session token from the request cookies, or null when absent. */
export function readSessionToken(req: Request): string | null {
  const cookies = parseCookies(req.headers.get("cookie"));
  for (const name of SESSION_COOKIE_NAMES) {
    const value = cookies[name];
    if (value !== undefined && value !== "") {
      return value;
    }
  }
  return null;
}

/** Serializes one set-cookie string with the BFF's fixed path, security, and SameSite policy. */
export function serializeCookie(name: string, value: string, options: CookieOptions): string {
  const parts = [`${name}=${value}`, "Path=/"];
  if (name.startsWith("__Host-") || options.secure) {
    parts.push("Secure");
  }
  if (options.httpOnly) {
    parts.push("HttpOnly");
  }
  parts.push("SameSite=Strict");
  if (options.maxAgeSeconds !== undefined) {
    parts.push(`Max-Age=${options.maxAgeSeconds}`);
  }
  return parts.join("; ");
}

/** Serializes an expired cookie that makes the browser drop `name`. */
export function clearedCookie(name: string, secure: boolean, httpOnly: boolean): string {
  const parts = [`${name}=`, "Path=/", "Max-Age=0", "Expires=Thu, 01 Jan 1970 00:00:00 GMT"];
  if (name.startsWith("__Host-") || secure) {
    parts.push("Secure");
  }
  if (httpOnly) {
    parts.push("HttpOnly");
  }
  parts.push("SameSite=Strict");
  return parts.join("; ");
}
