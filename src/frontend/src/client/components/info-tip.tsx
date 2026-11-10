import { useId, useState } from "react";

import { useRenderLog } from "../lib/debug.ts";

/**
 * Usage explanation for one control: a circled i, revealed on hover or focus only. One line of
 * usage per placement, never prose. Keyboard reachable; aria-describedby wires the button to the
 * tooltip text. No transition, so it is instant under prefers-reduced-motion.
 */
export function InfoTip({ text }: { text: string }) {
  useRenderLog("InfoTip");
  const id = useId();
  const [open, setOpen] = useState(false);
  return (
    <span
      className="info-tip"
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <button
        type="button"
        className="info-tip-btn"
        aria-label="More info"
        aria-describedby={id}
        onFocus={() => setOpen(true)}
        onBlur={() => setOpen(false)}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            setOpen(false);
          }
        }}
      >
        <span aria-hidden="true">i</span>
      </button>
      <span role="tooltip" id={id} className="info-tip-bubble" hidden={!open}>
        {text}
      </span>
    </span>
  );
}
