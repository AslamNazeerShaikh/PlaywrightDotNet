import type { ReactNode } from "react";

import { useRenderLog } from "../lib/debug.ts";
import { Scramble } from "./scramble.tsx";

/**
 * Shared deck furniture: the framed module shell, stat readouts, stable-code tags, raw JSON blocks,
 * and the reference tables. One module = one framed panel with corner brackets (see globals.css);
 * content inside always wraps or scrolls within its cell, never pushing the grid wider.
 */

export function Module({
  code,
  title,
  note,
  span = "span-4",
  children,
}: {
  code: string;
  title: string;
  note?: ReactNode;
  span?: string;
  children: ReactNode;
}) {
  useRenderLog(`Module:${title}`);
  return (
    <section className={`module ${span}`} aria-label={title}>
      <div className="module-head">
        <span className="module-code">{code}</span>
        <h2 className="module-title">
          <Scramble text={title} duration={825} ariaHidden />
        </h2>
        {note !== undefined && <span className="module-note">{note}</span>}
      </div>
      {children}
    </section>
  );
}

export function Stat({
  label,
  value,
  tone,
}: {
  label: string;
  value: string;
  tone?: "ok" | "bad" | "warn";
}) {
  useRenderLog(`Stat:${label}`);
  return (
    <div className="stat">
      <div className="stat-label">{label}</div>
      <div className={`stat-value${tone !== undefined ? ` ${tone}` : ""}`}>{value}</div>
    </div>
  );
}

export function CodeTag({ code, tone }: { code: string; tone?: "ok" | "bad" | "warn" }) {
  return <span className={`code-tag${tone !== undefined ? ` ${tone}` : ""}`}>{code}</span>;
}

export function JsonBlock({ value, label }: { value: unknown; label: string }) {
  useRenderLog(`JsonBlock:${label}`);
  return (
    <pre className="json" aria-label={label}>
      {JSON.stringify(value, null, 2)}
    </pre>
  );
}

/** "1 page", "3 pages": counts in readouts read as English. */
export function plural(count: number, word: string): string {
  return `${count} ${word}${count === 1 ? "" : "s"}`;
}

/** Maps a stable error code to its tone for tags and log rows. */
export function codeTone(code: string): "ok" | "bad" | "warn" | undefined {
  if (code === "OK" || code === "HEALTHY" || code === "CREATED") {
    return "ok";
  }
  if (
    code === "VALIDATION_FAILED" ||
    code === "PROFILE_NAME_TAKEN" ||
    code === "DOTNET_UNREACHABLE" ||
    code === "BFF_UNREACHABLE"
  ) {
    return "warn";
  }
  if (code === "PROFILE_NOT_FOUND" || code === "UNKNOWN" || code === "BFF_NOT_FOUND") {
    return "bad";
  }
  return undefined;
}
