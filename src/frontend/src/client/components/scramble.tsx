import { useEffect, useState } from "react";

/**
 * Decryption text reveal: random glyphs lock into the target left to right. Reserved for mount-once
 * moments (brand mark, page titles) — never for live data. Honors reduced motion by rendering the
 * final text instantly, and exposes it via aria-label so assistive tech skips the scramble.
 */

// Full symbol rack, no alphanumerics: the scramble can never equal the target.
const GLYPHS = "!@#$%^&*()_+!@#$%^&*()_+-={}|:\"<>?/.,;'[]\\";

export function Scramble({
  text,
  duration = 975,
  className,
  ariaHidden = false,
}: {
  text: string;
  duration?: number;
  className?: string;
  /** The module frame already labels the card, so its title scramble hides. */
  ariaHidden?: boolean;
}) {
  // Open on glyphs so the first paint is already decrypting; the effect
  // below locks characters in left to right.
  const [display, setDisplay] = useState(() => fullScramble(text));

  useEffect(() => {
    if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
      setDisplay(text);
      return;
    }
    const start = performance.now();
    let frame = 0;
    const timer = setInterval(() => {
      frame += 1;
      const progress = Math.min(1, (performance.now() - start) / duration);
      const locked = Math.floor(progress * text.length);
      let out = "";
      for (let i = 0; i < text.length; i += 1) {
        const ch = text[i];
        if (ch === " " || i < locked) {
          out += ch;
        } else {
          out += GLYPHS[(frame * 7 + i * 13) % GLYPHS.length];
        }
      }
      setDisplay(out);
      if (progress >= 1) {
        clearInterval(timer);
      }
    }, 30);
    return () => clearInterval(timer);
  }, [text, duration]);

  return ariaHidden ? (
    <span className={className} aria-hidden="true">
      {display}
    </span>
  ) : (
    <span className={className} aria-label={text}>
      {display}
    </span>
  );
}

/** Every non-space character replaced by a deterministic glyph. */
function fullScramble(text: string): string {
  let out = "";
  for (let i = 0; i < text.length; i += 1) {
    out += text[i] === " " ? " " : GLYPHS[(i * 13) % GLYPHS.length];
  }
  return out;
}
