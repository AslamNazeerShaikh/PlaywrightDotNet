import type { ReactNode } from "react";

import { useRenderLog } from "../lib/debug.ts";

/**
 * Deck key system: legends decode color/shape wherever it carries meaning, the glossary expands
 * every abbreviation once, and Abbr marks in-text shorthands with native tooltips. One shared
 * vocabulary, no per-page drift.
 */

export interface LegendItem {
  key: string;
  swatch: ReactNode;
  text: string;
}

export function Legend({ label, items }: { label: string; items: LegendItem[] }) {
  useRenderLog("Legend");
  return (
    <div className="legend" role="list" aria-label={label}>
      {items.map((item) => (
        <span key={item.key} className="legend-item" role="listitem">
          {item.swatch}
          <span>{item.text}</span>
        </span>
      ))}
    </div>
  );
}

/** Colored dot: probe hits, status points. */
export function Dot({ color }: { color: string }) {
  return <span className="swatch-dot" style={{ backgroundColor: color }} aria-hidden="true"  id="legend-span-1"/>;
}

/** Rotated square: units on the tac-map. */
export function Diamond({ color }: { color: string }) {
  return <span className="swatch-diamond" style={{ borderColor: color }} aria-hidden="true"  id="legend-span-2"/>;
}

/** Wire sample: solid for live, dashed for unknown. */
export function Wire({ color, dashed = false }: { color: string; dashed?: boolean }) {
  return (
    <span
      className="swatch-wire"
      style={{ borderTopColor: color, borderTopStyle: dashed ? "dashed" : "solid" }}
      aria-hidden="true"
     id="legend-span-3"/>
  );
}

/** Hollow ring: HQ marker, range bands. */
export function Ring({ color }: { color: string }) {
  return <span className="swatch-ring" style={{ borderColor: color }} aria-hidden="true"  id="legend-span-4"/>;
}

/** X mark: failed probes, kills. Pure CSS bars, no glyph-font dependency. */
export function Cross({ color }: { color: string }) {
  return (
    <span
      className="swatch-cross"
      style={{ ["--cross-color" as string]: color }}
      aria-hidden="true"
     id="legend-span-5"/>
  );
}

export interface GlossaryTerm {
  abbr: string;
  full: string;
  hint: string;
}

export function Glossary({ terms }: { terms: GlossaryTerm[] }) {
  useRenderLog("Glossary");
  return (
    <dl className="glossary">
      {terms.map((t) => (
        <div key={t.abbr} className="glossary-row" id="legend-div-6">
          <dt id="legend-dt-7">
            <abbr title={t.full}>{t.abbr}</abbr>
          </dt>
          <dd id="legend-dd-8">
            <strong>{t.full}.</strong> {t.hint}
          </dd>
        </div>
      ))}
    </dl>
  );
}

/** In-text shorthand with a native tooltip. Use sparingly: first mention only. */
export function Abbr({ term, full }: { term: string; full: string }) {
  return (
    <abbr className="abbr" title={full}>
      {term}
    </abbr>
  );
}
