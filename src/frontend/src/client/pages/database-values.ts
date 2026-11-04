import type { DbColumnInfo, DbTableSchema } from "../lib/api.ts";

/** Longest cell text shown inline; the full value rides on `title`. */
export const CELL_MAX = 80;

/** Text shown in a grid cell: short values verbatim, long ones trimmed. */
export function cellText(value: unknown): string {
  const full = cellFull(value);
  return full.length > CELL_MAX ? `${full.slice(0, CELL_MAX)}…` : full;
}

/** Full cell text for the `title` tooltip. */
export function cellFull(value: unknown): string {
  if (value === null || value === undefined) {
    return "";
  }
  return typeof value === "string" ? value : JSON.stringify(value);
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
    if (!Number.isNaN(n) && raw.trim() !== "") {
      return n;
    }
  }
  return raw;
}
