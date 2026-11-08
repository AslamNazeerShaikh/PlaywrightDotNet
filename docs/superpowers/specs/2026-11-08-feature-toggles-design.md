# Feature toggles design

## Intent and why

The app grew a full login, role, and user system on top of what used
to be an open local scraper. Sign-in stays the default. On localhost
the operator sometimes wants the old behavior back: no login window,
every data page usable, no identity machinery. Five appsettings
toggles provide that. One master switch turns authentication off.
Four small switches silence logging, health, Excel export, and CSV
export independently.

## Goals

- `JobScrape:Features` in appsettings.json owns all five switches.
  The file is the only activator. No env var, no second condition.
- Authentication off restores pre-auth behavior: anonymous callers act
  as the local operator, login and identity CRUD go dark, the UI shows
  the operator deck with no login window.
- Idempotency and rate limiting stay enabled in every mode.
- Anonymous callers get their own quota bucket, separate from users.
- Logging, health, Excel, and CSV toggle independently with no
  cross effects.
- Every default preserves current behavior. Upgrading changes nothing
  unless an operator flips a switch.

## Non-goals

- No per-user or per-role flag targeting. One process, one setting.
- No runtime reload. Flags bind once at host startup and stay fixed.
- No public anonymous deployment design. Anonymous mode refuses to
  start on non-loopback binds (fail closed, logged).

## Anti-goals

- A half anonymous mode where some endpoints challenge and others do
  not, with no visible rule. All or local-operator.
- A UI that hides buttons while the API still serves the feature, or
  the reverse. The capability payload drives both sides from one
  source.

## Constraints

- .NET binds options at startup. Compile time reads do not exist.
- Fail closed everywhere. Bad values fail the host via
  ValidateOnStart, never one request.
- Shipped appsettings defaults are asserted by OptionsStartupTests.
  New keys default to current behavior so those tests keep passing.
- Application layer takes a plain `FeatureFlags` record, never
  `IOptions<T>`, per docs/14-application-patterns.md.
- No new packages.

## Approaches

**A. Full local operator (picked).** Anonymous callers become the
existing local operator with every permission. Data APIs behave as
before auth existed. Login, setup, users, and roles endpoints return
404. The UI skips login and shows operator nav. One anonymous quota
bucket keyed by IP. Smallest change matching the ask.

**B. Restricted anonymous grants (dropped).** A fixed grant set for
anonymous callers, e.g. runs read-write but no admin surface. More
code and a second permission model on a machine where the operator
already owns the SQLite file. Theater security, real maintenance.

**Auto-login as seed admin (dropped).** Kills the login window but
keeps identity, sessions, and audit alive. Contradicts the ask.

## Design

### Options shape (defaults preserve today)

```json
"Features": {
  "Authentication": true,
  "Logging": true,
  "Health": true,
  "CsvExport": true,
  "ExcelExport": true
}
```

`FeaturesOptions`, section `JobScrape:Features`, plain bools, no
cross-field rules beyond the loopback guard. `False` means the feature
is off. Anonymous mode is `Authentication: false` and nothing else.
Mapped once to a `FeatureFlags` record for the application layer.

### Anonymous mode, backend

- Auth services always register. Skipping them crashes startup
  (`UseAuthentication`, rate limiter, MCP challenge).
- A single authorization short-circuit grants everything when
  `Authentication` is false; the default pipeline runs untouched when
  true. No per-policy edits.
- `ICallerContext` returns the local operator when the flag is off.
  MCP tools and caller-based guards keep working under that actor.
- Login, setup-password, users, and roles endpoints return 404 with a
  feature-disabled problem. Handler registrations stay; the edge says
  no. Builder call: exact problem code string.
- The seed service skips admin user creation only. Roles and quota
  seeding stay, since quotas still enforce.
- `JwtOptionsValidator` skips the 32-byte secret demand when the flag
  is off. A missing secret with auth on still fails the host.
- Anonymous mode plus a non-loopback bind URL refuses startup with a
  logged error. My call; say so if you want it removed.
- MCP keeps challenging semantics through the substituted actor. No
  separate MCP switch.

### Anonymous mode, UI and BFF

- New anonymous `GET /api/v1/features` returns the five flags. The
  deck reads it on boot. No other config channel exists, so this one
  payload drives hidden pages, hidden export buttons, and the login
  skip. Health payload is not reused for this.
- When authentication is false the deck never renders the login
  window. Nav shows every operator page except users, roles, and
  login. Quotas page stays visible with the anonymous bucket.
- The BFF mints the CSRF cookie during anonymous session bootstrap,
  since anonymous writes still need it and login no longer issues it.
  Builder call: exact bootstrap shape.

### Quotas in anonymous mode

- New `QuotaScope.Anonymous` member. String backed, no migration.
- Remove the authenticated-only guard in the quota middleware. Key
  anonymous counters by client IP so all localhost traffic does not
  share one bucket with itself in a misleading way. Single user,
  single bucket in practice.
- Seed a default anonymous budget next to the existing two. Health
  and auth exemptions stay.

### Logging toggle

- `Logging: false` registers Serilog with no sinks. Registration
  itself stays or the request pipeline throws.
- `LoggingOptions.Directory` validation is skipped with logging off,
  since no directory is used. Existing file-asserting tests gain an
  off-mode case instead of changing the default.

### Health toggle

- `Health: false` removes the `api/v1/health` route. The handler and
  MCP `get_health` stay alive. State this in the note so the split is
  deliberate, not drift.
- Tests that hit health as a fixture gain the enabled default or an
  explicit override. The deck shows no health card in this mode.

### Export toggles

- `CsvExport` / `ExcelExport: false` skips that writer's
  registration, which needs a `FeaturesOptions` parameter on the
  use-case registration. The endpoint answers 404 for the disabled
  format. The hardcoded "use csv or xlsx" error text becomes dynamic.
- Format-less export defaults to CSV when enabled, XLSX when CSV is
  off and Excel is on, 404 when both are off. Builder call: exact
  fallback order copy.
- UI export buttons hide per the capability payload. The button
  handler already renders the 400 path, which now also covers 404.

### Tests

- Options unit rows for every new field and bound. Startup tests for
  invalid overrides and for shipped defaults.
- Endpoint tests: features payload shape; disabled-format 404s;
  login 404 in anonymous mode; anonymous quota counting by IP.
- Frontend tests: hidden nav and buttons under the flag; login skip.

### Docs

- Update the section lists in docs/02-architecture.md and
  docs/04-solution-and-tooling.md with the Features section.

## Left to the builder

- Exact problem code strings and user-facing copy.
- CSRF bootstrap request shape.
- Export fallback copy when both formats are off.
