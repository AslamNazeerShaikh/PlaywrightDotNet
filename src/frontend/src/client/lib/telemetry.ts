/**
 * Deck telemetry: latency samples and the ops event log. All helpers are pure (arrays in, capped
 * arrays out) so pages stay thin and tests stay exact. Sampling caps keep the deck bounded no
 * matter how long it runs.
 */

export interface Sample {
  label: string;
  ms: number;
  at: number;
  ok: boolean;
}

export interface LatencyStats {
  count: number;
  last: number;
  min: number;
  max: number;
  p50: number;
}

export interface LogEvent {
  at: string;
  kind: "ok" | "err" | "info";
  code: string;
  detail: string;
}

export const MAX_SAMPLES = 60;
export const MAX_EVENTS = 80;

/** Appends a sample, keeping the newest `cap` entries. */
export function pushSample(samples: Sample[], sample: Sample, cap: number = MAX_SAMPLES): Sample[] {
  return [...samples, sample].slice(-cap);
}

/** Summarizes samples; null when nothing has been measured yet. */
export function latencyStats(samples: Sample[]): LatencyStats | null {
  if (samples.length === 0) {
    return null;
  }
  const sorted = samples.map((s) => s.ms).sort((a, b) => a - b);
  const mid = Math.floor((sorted.length - 1) / 2);
  return {
    count: samples.length,
    last: samples[samples.length - 1]?.ms ?? 0,
    min: sorted[0] ?? 0,
    max: sorted[sorted.length - 1] ?? 0,
    p50: sorted[mid] ?? 0,
  };
}

/** Prepends an event (newest first), keeping the newest `cap` entries. */
export function pushEvent(
  events: LogEvent[],
  event: LogEvent,
  cap: number = MAX_EVENTS,
): LogEvent[] {
  return [event, ...events].slice(0, cap);
}

/** Formats a duration for dense readouts: sub-10 ms keeps one decimal. */
export function formatMs(ms: number): string {
  return ms < 10 ? `${ms.toFixed(1)} ms` : `${Math.round(ms)} ms`;
}
