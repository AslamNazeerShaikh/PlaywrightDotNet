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

/** CamelCase enum names as stable-code tags: "CoolingDown" → "COOLING_DOWN". */
export function constantCase(value: string): string {
  return value.replace(/([a-z])([A-Z])/g, "$1_$2").toUpperCase();
}

/** Maps a stable error code to its tone for tags and log rows. */
export function codeTone(code: string): "ok" | "bad" | "warn" | undefined {
  if (
    code === "OK" ||
    code === "HEALTHY" ||
    code === "CREATED" ||
    code === "UPDATED" ||
    code === "RUN_STARTED" ||
    code === "COMPLETED"
  ) {
    return "ok";
  }
  if (
    code === "VALIDATION_FAILED" ||
    code === "PROFILE_NAME_TAKEN" ||
    code === "SESSION_NAME_TAKEN" ||
    code === "SESSION_RENAME_BLOCKED" ||
    code === "RUN_IN_PROGRESS" ||
    code === "QUOTA_EXCEEDED" ||
    code === "SOURCE_COOLING_DOWN" ||
    code === "SESSION_COOLING_DOWN" ||
    code === "CANCELLED" ||
    code === "DOTNET_UNREACHABLE" ||
    code === "BFF_UNREACHABLE"
  ) {
    return "warn";
  }
  if (
    code === "PROFILE_NOT_FOUND" ||
    code === "RUN_NOT_FOUND" ||
    code === "FAILED" ||
    code === "UNKNOWN" ||
    code === "BAD_RESPONSE" ||
    code === "BFF_NOT_FOUND"
  ) {
    return "bad";
  }
  return undefined;
}

/**
 * A labeled form field that renders the server's field errors for `name` (from a VALIDATION_FAILED
 * problem) under its input, so every rule the API enforces is shown where it applies.
 */
export function Field({
  label,
  name,
  errors,
  children,
}: {
  label: string;
  name: string;
  errors: Record<string, string[]>;
  children: ReactNode;
}) {
  const messages = errors[name] ?? [];
  return (
    <label className="field">
      <span>{label}</span>
      {children}
      {messages.map((m) => (
        <span key={m} className="field-error" role="note" data-field={name}>
          {m}
        </span>
      ))}
    </label>
  );
}
