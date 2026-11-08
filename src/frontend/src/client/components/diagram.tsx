/**
 * Shared SVG diagram vocabulary: semantic tones and the notched arrow markers used by every inline
 * diagram. Colors read the same theme tokens as the deck, so light and dark stay in sync. Marker
 * ids are namespaced per diagram because all diagrams render in one document.
 */
export type DiagramTone = "green" | "cyan" | "violet" | "amber" | "red" | "gray";

export const TONE: Record<DiagramTone, { stroke: string; fill: string; text: string }> = {
  green: { stroke: "var(--sig)", fill: "var(--diagram-green-soft)", text: "var(--sig)" },
  cyan: { stroke: "var(--brand-2)", fill: "var(--diagram-cyan-soft)", text: "var(--brand-2)" },
  violet: {
    stroke: "var(--brand-3)",
    fill: "var(--diagram-violet-soft)",
    text: "var(--brand-3)",
  },
  amber: { stroke: "var(--amber)", fill: "var(--diagram-amber-soft)", text: "var(--amber)" },
  red: { stroke: "var(--red)", fill: "var(--diagram-red-soft)", text: "var(--red)" },
  gray: { stroke: "var(--line-hi)", fill: "var(--diagram-gray-soft)", text: "var(--dim)" },
};

const TONES: DiagramTone[] = ["green", "cyan", "violet", "amber", "red", "gray"];

/** Marker id for a namespaced diagram tone. */
export function markerId(prefix: string, tone: DiagramTone): string {
  return `${prefix}-arrow-${tone}`;
}

/** Marker id for the reverse end of a bidirectional connector. */
export function startMarkerId(prefix: string, tone: DiagramTone): string {
  return `${prefix}-start-${tone}`;
}

/** Normalizes a PRAGMA on_delete wire value: trims whitespace, uppercases. Null stays null. */
export function actionLabel(onDelete: string | null | undefined): string | null {
  return onDelete?.trim().toUpperCase() ?? null;
}

/** Maps a raw on_delete wire value to its diagram tone. Trims via actionLabel. */
export function accentTone(onDelete: string | null | undefined): DiagramTone {
  const action = actionLabel(onDelete);
  if (action === "CASCADE") {
    return "red";
  }
  if (action === "RESTRICT") {
    return "green";
  }
  return "gray";
}

/** Notched arrowheads, one per tone. `userSpaceOnUse` keeps them a fixed 8px at any stroke width. */
export function DiagramDefs({ prefix }: { prefix: string }) {
  return (
    <defs>
      {TONES.map((tone) => (
        <marker
          key={tone}
          id={markerId(prefix, tone)}
          markerWidth="8"
          markerHeight="8"
          refX="2"
          refY="4"
          orient="auto"
          markerUnits="userSpaceOnUse"
        >
          <path d="M0,0 L8,4 L0,8 L2,4 z" style={{ fill: TONE[tone].stroke }} />
        </marker>
      ))}
      {TONES.map((tone) => (
        <marker
          key={`start-${tone}`}
          id={startMarkerId(prefix, tone)}
          markerWidth="8"
          markerHeight="8"
          refX="2"
          refY="4"
          orient="auto-start-reverse"
          markerUnits="userSpaceOnUse"
        >
          <path d="M0,0 L8,4 L0,8 L2,4 z" style={{ fill: TONE[tone].stroke }} />
        </marker>
      ))}
    </defs>
  );
}
