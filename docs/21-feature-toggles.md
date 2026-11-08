# Feature toggles

Source of truth for operator behavior. Implementation: `FeaturesOptions`
(`JobScrape:Features`), mapped once to the plain `FeatureFlags` record.
Flags bind at host startup and stay immutable per process. Every default
preserves current behavior. The capability endpoint
`GET /api/v1/features` is anonymous and reports all five flags; the
cockpit does not consume it yet (anonymous-mode follow-up).

## State matrix

| Toggle | appsettings key (default) | ON | OFF |
|---|---|---|---|
| Authentication | `Authentication: true` | Login, setup, users, roles active; JWT required; admin seed runs; JWT secret validated | Reserved, no effect (takes effect in the anonymous-mode follow-up) |
| Logging | `Logging: true` | Console, `.jsonl`, and `.log` sinks; `JobScrape:Logging:Directory` required | No sinks at all, including config-declared `Serilog:WriteTo`; Serilog stays registered and disposed; Directory validation skipped |
| Health | `Health: true` | `GET /api/v1/health` 200; MCP `get_health` alive | Route answers 404 `FEATURE_DISABLED`; handler and MCP `get_health` stay alive |
| CSV export | `CsvExport: true` | CSV writer registered; format-less export defaults to CSV | Writer absent; explicit `format=csv` 404s; format-less falls back to XLSX, or 404 if that is off too |
| Excel export | `ExcelExport: true` | XLSX writer registered | Writer absent; explicit `format=xlsx` 404s; format-less falls back to CSV, or 404 if that is off too |

## Edge rules

- Both exports off: every export request 404s `FEATURE_DISABLED`,
  regardless of format.
- Group validation runs before the feature gate: a bad `group` value
  400s even when the format is disabled.
- Unparseable `format` values 400 regardless of flags; only recognized
  disabled formats 404.
- Non-boolean flag values fail startup binding (`InvalidOperationException`).
- UI gating, anonymous callers, and the anonymous quota bucket belong
  to the anonymous-mode follow-up, not to these flags.
