import type { DbColumnInfo, DbTableSchema } from "../lib/api.ts";

/** Longest cell text shown inline; the full value rides on `title`. */
export const CELL_MAX = 80;

/** .NET DateTime ticks: 100 ns intervals since 0001-01-01T00:00:00Z. */
const UNIX_EPOCH_TICKS = 621355968000000000n;
const TICKS_PER_MS = 10000n;
const MIN_TICKS = 599266080000000000n; // 1900-01-01
const MAX_TICKS = 725811840000000000n; // 2200-01-01

/** Timestamp columns are stored as .NET ticks by EF Core; the suffix is stable across tables. */
export function isTimestampColumn(name: string): boolean {
  return /_(at|until)$/i.test(name);
}

/**
 * Formats a .NET tick count as a UTC wall-clock. Returns null for blank, non-numeric, or
 * out-of-range values so JSON blobs and plain integers keep their raw display.
 */
export function formatTimestamp(value: unknown): string | null {
  const ticks = toTicks(value);
  if (ticks === null || ticks < MIN_TICKS || ticks > MAX_TICKS) {
    return null;
  }
  const date = new Date(Number((ticks - UNIX_EPOCH_TICKS) / TICKS_PER_MS));
  if (Number.isNaN(date.getTime())) {
    return null;
  }
  const iso = date.toISOString();
  return `${iso.slice(0, 10)} ${iso.slice(11, 19)} UTC`;
}

function toTicks(value: unknown): bigint | null {
  if (typeof value === "bigint") {
    return value;
  }
  if (typeof value === "number") {
    return Number.isInteger(value) ? BigInt(value) : null;
  }
  if (typeof value === "string" && /^\d+$/.test(value.trim())) {
    return BigInt(value.trim());
  }
  return null;
}

function rawText(value: unknown): string {
  if (value === null || value === undefined) {
    return "";
  }
  return typeof value === "string" ? value : JSON.stringify(value);
}

/** Text shown in a grid cell: timestamps read as UTC, short values verbatim, long ones trimmed. */
export function cellText(value: unknown, column?: string): string {
  const stamp = column !== undefined && isTimestampColumn(column) ? formatTimestamp(value) : null;
  const full = stamp ?? rawText(value);
  return full.length > CELL_MAX ? `${full.slice(0, CELL_MAX)}…` : full;
}

/** Full cell text for the `title` tooltip; timestamp columns keep the stored ticks too. */
export function cellFull(value: unknown, column?: string): string {
  const raw = rawText(value);
  if (raw === "" || column === undefined || !isTimestampColumn(column)) {
    return raw;
  }
  const stamp = formatTimestamp(value);
  return stamp === null ? raw : `${stamp} · ${raw} ticks`;
}

/** Values for an insert: blanks are omitted so defaults apply. */
export function readRowForm(data: FormData, schema: DbTableSchema): Record<string, unknown> {
  const values: Record<string, unknown> = {};
  for (const column of schema.columns) {
    const raw = data.get(column.name);
    if (raw === null || raw instanceof File) {
      continue;
    }
    if (raw === "") {
      continue;
    }
    values[column.name] = coerce(column, raw);
  }
  return values;
}

/** One form value: blanks clear nullable columns and stay blank otherwise. */
export function readValue(raw: FormDataEntryValue | null, column: DbColumnInfo): unknown {
  if (raw === null || raw instanceof File) {
    return null;
  }
  if (raw === "") {
    return column.notNull ? "" : null;
  }
  return coerce(column, raw);
}

/** Raw input to a JSON-safe value using the live column type. */
export function coerce(column: DbColumnInfo, raw: string): unknown {
  if (raw === "" && !column.notNull) {
    return null;
  }
  const type = column.type.toUpperCase();
  if (type.includes("BOOL")) {
    if (raw.toLowerCase() === "true") {
      return true;
    }
    if (raw.toLowerCase() === "false") {
      return false;
    }
    return raw;
  }
  if (
    type.includes("INT") ||
    type.includes("REAL") ||
    type.includes("NUM") ||
    type.includes("FLOA") ||
    type.includes("DOUB")
  ) {
    const n = Number(raw);
    if (raw.trim() !== "" && !Number.isNaN(n)) {
      // Integers above 2^53 round in JS; keep the exact digits for SQLite to coerce.
      return Number.isInteger(n) && !Number.isSafeInteger(n) ? raw : n;
    }
  }
  return raw;
}
