# KPI and searchable-filter enhancement — 20 October 2026

Branch: `feature/sandpiper-command-center-v2`. Baseline: `43139e8`.
The existing ASP.NET Core 10 / React solution and original Frontier routes remain.
No main merge, deployment, extension publication, database or infrastructure change.

## All five enhancements delivered

1. Shared backend distinct-case counts and case execution coverage, with the original
   point metrics retained. Suites/configurations cannot multiply a case identity.
2. Reusable searchable comboboxes replace external search fields/native selects.
   Dynamic plans, full suite paths, indentation, All Suites, keyboard navigation,
   Enter/Escape, focus restoration and loading/empty/error states require no UI library.
3. Total/Open/Completed scoped linked Bug KPIs deduplicate inspected case relationships
   and result associatedBugs. Missing collections, unresolved references or unavailable
   metadata withhold totals. Work-item IDs are resolved only in the configured ADO
   organization. Unknown categories withhold open/completed counts.
4. Overview shows Re-executed Test Cases with case → point → baseline attempt/executor
   drill-down. Technical repeated-point/repeat-event counters remain available.
5. Explicit Loading, verified numeric (including genuine zero), Incomplete evidence,
   Unavailable, Permission denied, Awaiting baseline and Not Assessed states. Overview
   loads automatically after a valid explicit baseline/window, with cancellation,
   debounce, same-scope reuse and response scope/date checks. No null-to-zero fallback.

The Overview groups Test Coverage, Execution Status, and Quality and Retesting. Cards
show units, formula tooltips and record drill-downs; additional period tables collapse.
DSR/TCR previews and Excel/PDF use the same new metrics. New report calculation version
is `v2-2026-10-20.2`; `.1` remains an accepted checksum-verified import schema.

## Before / after rules

| Metric | Before | After |
|---|---|---|
| Total Test Cases | Not calculated separately | Distinct mapped Test Case IDs in current selected scope |
| Total Test Points | Distinct point IDs | Unchanged, technical reconciliation retained |
| Executed Test Cases | Not calculated separately | Cases with at least one current executed-status point |
| Execution Coverage | Point progress prominent | Explicit case-based executed cases / total cases |
| Point Execution Progress | Applicable executed points / applicable points | Unchanged, explicitly point-based, N/A excluded |
| Passed/Failed/Blocked/Pending/N/A | Point outcomes | Unchanged point units; no mixed-configuration case status inferred |
| Primary / Secondary Pass | Agreed point denominators | Unchanged, clearly point-based |
| Re-executed Test Cases | Overview displayed repeated points | Distinct case IDs with a point having ≥2 qualifying baseline attempts |
| Technical re-execution | Repeated points / repeat events | Retained; incomplete evidence withholds affected totals |
| Total Defects | No business KPI | Distinct inspected case/result-linked Bug IDs, not project-wide Bugs |
| Open / Completed | Open result-linked Bugs | Inspected categories; Completed and Removed distinguished |
| Readiness | Text/disabled rules | Not Assessed unless approved thresholds are explicitly enabled |

Current executed point statuses include Blocked and N/A, preserving the agreed policy.
Case coverage therefore includes a case with an N/A executed-status point. Historical
qualifying attempts remain distinct completed retained results, including Blocked but
excluding N/A. These are different concepts. Unclassified cases without a known
executed point withhold executed-case/coverage totals; identity totals can still be
known. Empty scope has zero identities and an unavailable percentage, not 0%/100%.

Tester semantics are preserved: current cards use assignment, history uses actual
executor, and defect totals cover the selected plan/subtree across all testers. Daily
Bug activity uses Created By/revision/retest actors. Total defects can differ from
open + completed because Removed remains separately visible in source categories.

## Confirmed code-level zero paths

Baseline references below are at commit `43139e8`, reproduced with controlled tests.
They do not establish the cause of the corporate incident without live evidence.

- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionCalculator.cs:61` marked
  history complete merely when missing dates/point-link counters were zero. An empty
  history passed and returned zero even when a current latest-result reference was
  absent from it. The new evaluator reconciles expected results/current execution.
- `Sandpiper.QA.Dashboard.Server/Services/WorkItemEvidenceService.cs:40` used `All(...)`
  on an empty result collection, which returned true. It did not check identified
  cases, explicit case relation arrays or expected retained history before open=0.
  Complete totals now require those checks plus supported metadata/result Bug arrays.
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx:146` used text for nulls,
  not numeric zero. It displayed backend zero when the backend claimed completeness.
  Evidence also required a separate manual baseline/load action. The new component
  validates the baseline, automatically requests valid scope and displays evidence state.
- Upstream permission exceptions previously became generic 502 errors. Sanitized
  403/PermissionDenied now survives backend and typed client mapping; WIT permission
  failures retain their own evidence state rather than empty numeric results.
- No numeric readiness/zero assignment was found in this baseline. Readiness was text.
  Disabled thresholds now always force Not Assessed; request failures remain explicit.

The actual deployed version, R2 unspecified outcomes, corporate permissions and retained
history still require VDI verification. No production incident cause is claimed proven.

## Files and purpose

`docs/v2-changed-files.md` lists every exact modified path. Main implementation changes:

- `Models/BusinessMetrics.cs` plus additive fields in `Analytics.cs`,
  `ExecutionIntelligence.cs`, `QualityEvidence.cs`: business/evidence contracts.
- `Services/Analytics/CaseCalculator.cs`, `HistoryEvidenceEvaluator.cs`,
  `ScopeProjector.cs`, `ExecutionCalculator.cs`: shared case/point/baseline rules.
- `Services/WorkItemEvidenceService.cs`, `AzureDevOpsClient.cs`: scoped Bug identities,
  deduplication, state metadata, organization-bound relationships and completeness.
- `Services/WorkspaceEvidenceService.cs`, `ExecutionHistoryService.cs`,
  `Analytics/QualityCalculator.cs`, `Program.cs`: preflight dates, readiness and errors.
- `Services/Reporting/ReportService.cs`: versioned business KPI exports and case links.
- `src/components/OverviewKpis.tsx`, `SearchableCombobox.tsx`: grouped cards, states,
  reusable search control and matching evidence drawers.
- `src/app/CommandCenter.tsx`, `ReportingContext.ts`, `src/services/adoApi.ts`,
  `src/v2.css`: integration, hierarchy options, preserved filters and typed scope checks.
- `src/components/ActivityPanel.tsx`, `QualityPanel.tsx`, `ReportPanel.tsx`: technical
  counters retained and shared business metrics presented consistently.
- Backend, client and browser regression files plus repository reporting/validation docs.

Backend paths above are within `Sandpiper.QA.Dashboard.Server`; frontend paths within
`sandpiper.qa.dashboard.client`. Existing point API fields remain intact. Optional new
report fields omit null defaults to preserve older frozen-record structures.

## Validation and Visual Studio 2026 checks

Solution build: zero warnings/errors. TypeScript/Vite and ESLint pass. **68 backend,
12 client and 8 Chromium browser tests pass**, including existing Frontier plan routes.
Browser fixtures live only in test interception; no production mock/fallback exists.
Local packaging does not establish installed-extension connectivity.

A fresh real Vite/API smoke check returned Healthy through the proxy. Local sign-in
was unavailable, ADO discovery returned 503, and an invalid baseline returned 400.
No source payload was displayed or stored; temporary servers were stopped. Corporate
ADO reconciliation and interactive Visual Studio F5 remain VDI validation steps.

1. Check out this feature branch. Restore the .NET 10 solution and run
   `npm --prefix sandpiper.qa.dashboard.client ci`. Sign into Visual Studio with your
   authorized organizational account. Use ignored local settings/environment variables.
2. Open `Sandpiper.QA.Dashboard.slnx` in Visual Studio 2026, select the server `http`
   profile and run. Vite serves port 64423 and proxies `/api` to port 5243. For separate
   startup use the existing backend profile and root `npm run dev`.
3. Check sign-in and real plan discovery. Token acquisition alone is not authorization.
   Select Frontier AI 819259 and R2 839718, then a parent/leaf suite. Reconcile distinct
   case IDs separately from point IDs against ADO; never force unspecified to Pending.
4. Test one case in multiple configurations/suites. It remains one Total Case; point
   counts remain distinct. Case execution coverage increments once after an executed
   point. Outcome/pass-rate cards must still count points.
5. Enter explicit offset-aware period and baseline dates. Three distinct qualifying
   results on one point mean one repeated case, one repeated point, two repeat events.
   A second repeated point of that case must not add another case. Inspect IDs/times/
   actual executors in the evidence drawer, including earlier baseline attempts.
6. Compare unique linked Bug IDs and inspected categories with Total/Open/Completed
   drill records. Check duplicate links, case-only links, missing relation arrays,
   inaccessible WIT data and source timestamps. Missing evidence must never prove zero.
7. Test search, keyboard selection/Escape/focus, All Suites and plan-reset behavior.
   Keep thresholds disabled unless approved: readiness must be Not Assessed. Generate
   DSR/TCR drafts and compare Business KPIs in preview/Excel/PDF to the same frozen data.
8. Run build/lint/tests using README commands. Corporate reconciliation and installed
   extension testing still require approved access, endpoint and authentication.

No new case-level Passed/Failed policy or readiness thresholds were introduced. Future
mixed-configuration outcome aggregation, custom Bug-link semantics or threshold changes
need an explicit agreed policy. No further approval is needed for the implemented work.
