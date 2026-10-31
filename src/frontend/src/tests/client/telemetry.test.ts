import { describe, expect, test } from "bun:test";

import {
  formatMs,
  latencyStats,
  type LogEvent,
  pushEvent,
  pushSample,
} from "../../client/lib/telemetry.ts";

describe("telemetry", () => {
  test("pushSample appends and caps the window", () => {
    const base = { label: "health", at: 0, ok: true };
    let samples = [
      { ...base, ms: 1 },
      { ...base, ms: 2 },
    ];
    samples = pushSample(samples, { ...base, ms: 3 }, 2);

    expect(samples.map((s) => s.ms)).toEqual([2, 3]);
  });

  test("latencyStats summarizes, null on empty", () => {
    expect(latencyStats([])).toBeNull();

    const stats = latencyStats([
      { label: "h", ms: 4, at: 1, ok: true },
      { label: "h", ms: 1, at: 2, ok: true },
      { label: "h", ms: 9, at: 3, ok: false },
    ]);

    expect(stats).toEqual({ count: 3, last: 9, min: 1, max: 9, p50: 4 });
  });

  test("pushEvent prepends newest-first and caps", () => {
    const e = (code: string): LogEvent => ({ at: "00:00:00", kind: "ok", code, detail: code });
    const events: LogEvent[] = [e("A")];
    const capped = pushEvent(events, e("B"), 2);

    expect(capped.map((x) => x.code)).toEqual(["B", "A"]);
  });

  test("formatMs keeps one decimal under 10 ms", () => {
    expect(formatMs(4.26)).toBe("4.3 ms");
    expect(formatMs(41.6)).toBe("42 ms");
  });
});
