# Ops-Deck Screenshots

Reference render of the control plane captured at 1920 width with full-page height. Each section shows the same page in light and dark side by side. The deck defaults to light. The **THEME** button in the command bar flips it.

Files live in this folder as `NN-<page>-light.png` and `NN-<page>-dark.png`. Captured against the local API on `https://localhost:5208` through the BFF on `https://127.0.0.1:3000`.

## 01 Overview

Central dashboard. Engine state, proxy latency, tac-map, endpoint catalog, and killfeed at a glance. Start here to confirm the plane is live before you drill into a tab.

<div style="display:flex;gap:12px;">
<img src="./01-overview-light.png" width="49%" alt="Overview page in light theme" />
<img src="./01-overview-dark.png" width="49%" alt="Overview page in dark theme" />
</div>

## 02 Engine Health

Probe control and full health payload. Latency stats, sample history, and what the numbers mean. Use it to check one probe in isolation when the overview looks off.

<div style="display:flex;gap:12px;">
<img src="./02-health-light.png" width="49%" alt="Engine Health page in light theme" />
<img src="./02-health-dark.png" width="49%" alt="Engine Health page in dark theme" />
</div>

## 03 Search Profiles

Create and edit search profiles. The form shows server field errors inline. The table persists in SQLite. Select a row for the detail panel. Use it to define what a run searches for.

<div style="display:flex;gap:12px;">
<img src="./03-profiles-light.png" width="49%" alt="Search Profiles page in light theme" />
<img src="./03-profiles-dark.png" width="49%" alt="Search Profiles page in dark theme" />
</div>

## 04 Sources

One card per job source. Compliance tier and gate, daily quota meters, policy caps, and circuit state. Read-only by design. Use it to check whether a source accepts work before you start a run.

<div style="display:flex;gap:12px;">
<img src="./04-sources-light.png" width="49%" alt="Sources page in light theme" />
<img src="./04-sources-dark.png" width="49%" alt="Sources page in dark theme" />
</div>

## 05 Sessions

Browser-profile sessions. Create one identity per source and watch launch readiness. Login and probe actions wait on the session manager and say so. Use it to prepare the session a run launches with.

<div style="display:flex;gap:12px;">
<img src="./05-sessions-light.png" width="49%" alt="Sessions page in light theme" />
<img src="./05-sessions-dark.png" width="49%" alt="Sessions page in dark theme" />
</div>

## 06 Runs

Start a scrape, follow it live by polling, cancel it, and read its postings. Start form, history table, stage track with page and posting meters, and postings table with CSV and XLSX export. Use it to do the core loop.

<div style="display:flex;gap:12px;">
<img src="./06-runs-light.png" width="49%" alt="Runs page in light theme" />
<img src="./06-runs-dark.png" width="49%" alt="Runs page in dark theme" />
</div>

## 07 Topology

How the cockpit is wired. Request lifecycle sequence, component map of the two processes, and layer contract. Use it to trace where a request goes when a probe fails.

<div style="display:flex;gap:12px;">
<img src="./07-topology-light.png" width="49%" alt="Topology page in light theme" />
<img src="./07-topology-dark.png" width="49%" alt="Topology page in dark theme" />
</div>

## 08 Database

Browse every user table in the SQLite store. Live schema, paged rows, and CRUD. The diagram draws from live foreign keys. Use it to inspect what runs actually stored.

<div style="display:flex;gap:12px;">
<img src="./08-database-light.png" width="49%" alt="Database page in light theme" />
<img src="./08-database-dark.png" width="49%" alt="Database page in dark theme" />
</div>

## 09 Users

The signed-in account for every caller (roles, permissions, password change), plus the admin surface when the token carries user visibility: list, create with a temporary password, role assign and revoke. Use it to manage who can sign in and what they hold.

<div style="display:flex;gap:12px;">
<img src="./09-users-light.png" width="49%" alt="Users page in light theme" />
<img src="./09-users-dark.png" width="49%" alt="Users page in dark theme" />
</div>

## 10 Roles

Role catalog with create, plus per-role permission grants across the catalog keys. Grant and revoke are one checkbox each and the toggle disables while its own request flies. Use it to review and adjust what each role grants.

<div style="display:flex;gap:12px;">
<img src="./10-roles-light.png" width="49%" alt="Roles page in light theme" />
<img src="./10-roles-dark.png" width="49%" alt="Roles page in dark theme" />
</div>

## 11 Quotas

Usage quota definitions with per-user limits and fixed windows, plus an editor per scope. Use it to adjust how much of each quota a user may spend in a window.

<div style="display:flex;gap:12px;">
<img src="./11-quotas-light.png" width="49%" alt="Quotas page in light theme" />
<img src="./11-quotas-dark.png" width="49%" alt="Quotas page in dark theme" />
</div>
