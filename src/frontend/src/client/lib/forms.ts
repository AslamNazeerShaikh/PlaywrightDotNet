/**
 * Uncontrolled-form readers: every create/update form is read from FormData on submit
 * (happy-dom-safe, no per-keystroke state). Blank numbers become null; blank lists become [].
 */

export function readText(data: FormData, key: string): string {
  return String(data.get(key) ?? "").trim();
}

export function readNumber(data: FormData, key: string): number | null {
  const raw = readText(data, key);
  if (raw === "") {
    return null;
  }
  const value = Number(raw);
  return Number.isFinite(value) ? value : null;
}

export function readList(data: FormData, key: string): string[] {
  return readText(data, key)
    .split(",")
    .map((item) => item.trim())
    .filter((item) => item !== "");
}
