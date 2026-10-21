# Unified reporting delivery — 21 October 2026

Branch: `feature/sandpiper-command-center-v2`.
Base commit: `dd3df30c4159337bf7264255dc0c8db6000daa5e`.
The user conditionally authorized feature-branch commit/push on 21 October 2026,
subject to review and verification. This document accompanies that authorized
delivery; the exact commit is available through Git history and the delivery
response. No PR, main merge, infrastructure deployment, extension publication or
corporate ADO configuration change is authorized or performed.

## Incremental implementation

| Gate concern | Implementation |
| --- | --- |
| A — source normalization | Existing dynamic paginated plans/suites/points preserved; suite requirement ID/type exposed; exact point identity and re-execution corrections retained |
| B — shared scope | Backend plan/subtree/category/current-outcome projection; scoped evidence cache; assignment and actual-performer semantics retained |
| C — execution/productivity | Same scope forwarded to paginated historical activity; selected-range, baseline totals and period-repeat activity remain separate |
| D — DSR/TCR | Frozen scope metadata, versioned portable evidence, saved-report selection and stable data exports without ADO re-query |
| E — quality | Existing Created By attribution and result-linked verification retained; same category/outcome point scope applied before work-item evidence collection |
| F — UI/exports/validation | Five searchable selectors, complete-calendar-month presets, table Excel/PDF exports, module browser checks and VDI handoff |

No replacement application, new dependency, production KPI constants or fabricated
source data has been introduced. Dynamic discovery still supports both initially
named plans and future plans; plan IDs are not an application allowlist.

## Reporting semantics

All eight primary modules share Test Plan, suite subtree, testing category, current
outcome, tester and date context. Current point/case views use current assignment;
execution/productivity uses result runBy; bug creation uses ADO System.CreatedBy.
Quality case/result scope is selected before enrichment. Historical work is not
filtered by today's assignee merely because a different person executed it.

The global Outcome selector selects points by their **current** source outcome.
Historical activity then covers those points; it is not an assertion that their
past outcomes or membership were the same. Result-outcome searches remain available
in the history/drill tables. Both the UI and frozen evidence disclose current
membership as the historical-selection basis. Distinct cases, points and attempts
are never interchangeable. Existing KpiCalculator formulas are unchanged.

- Current state: latest point outcomes collected at RetrievedAt.
- Baseline: qualifying retained execution counts over [baselineFrom, activity.To).
- Activity: qualifying retained result events in [From inclusive, To exclusive).
- Historical as-of state: **unavailable** without authenticated point-state evidence
  captured at that cutoff. Current state is never backdated to the report's end.
- Preserved report: frozen collected state and calculations, with original filters
  and dates. Reopening does not query ADO. Imported JSON remains explicitly untrusted.

Today, Yesterday and Release-to-Date remain available for compatibility. Required
presets Last 7 Days, Last Month, Last 2 Months and Custom Date are available. The
last two month presets are previous complete calendar months, with each boundary
at the configured UK cutoff on the first day. UTC offsets are calculated separately
at each boundary, including DST. For example, Last Month selected in November 2026
is [1 October 18:00 BST, 1 November 18:00 GMT). Custom Date retains an explicit
baseline; automatic activity baselines track the selected period and are never
represented as authoritative release starts. A release start still requires user
confirmation and source/reference. Selecting a different plan clears confirmation.

## Testing-category configuration

Configure `AzureDevOps:SuiteCategories` in approved environment-specific settings
(for local Visual Studio development, the existing project user-secrets/settings
workflow). Do not put organization credentials in tracked configuration.

Each array entry has these fields:

| Field | Type | Meaning |
| --- | --- | --- |
| PlanId | positive integer | Actual selected ADO plan ID |
| SuiteId | positive integer | Actual ADO suite ID from the retrieved tree |
| Category | nonblank string, up to 80 characters | Approved category, e.g. Targeted Regression |
| IncludeDescendants | boolean, default true | Apply to descendants unless a nearer rule overrides it |

Use actual approved IDs from the VDI, not illustrative or invented IDs. With no
rules, points are Unclassified. Rules are plan-specific. Unknown configured suite
IDs and duplicate plan/suite rules fail explicitly rather than silently producing
a verified empty category. No folder-name heuristics are used. A point appearing
in multiple memberships can belong to multiple categories; filtering intersects
category membership with the selected subtree before deduplicating point IDs.

Requirement-based suites expose the source requirement/story ID and suite type
in Test Explorer and exports. The story ID links to the configured ADO project.
Targeted Regression is configured by suite ID; no Pre-Test/Post-Test folder or
test-plan-specific special case is required. Suite title and requirement metadata
are evidence, not a classification guessed from story text.

Microsoft's [suite API contract](https://learn.microsoft.com/en-us/rest/api/azure/devops/testplan/test-suites/get-test-suites-for-plan?view=azure-devops-rest-7.1)
defines continuation-based retrieval; [suite metadata](https://learn.microsoft.com/en-us/rest/api/azure/devops/testplan/test-suites/get?view=azure-devops-rest-7.1)
defines parentSuite, suiteType and requirementId. Source traversal preserves nested
and flat parent relationships and rejects cycles/conflicting identities.

## Reproducible reports and exports

DSR/TCR generation freezes current collection, activity, quality evidence and scope
metadata with a checksum and calculation version `v2-2026-10-21.3`. Older .1/.2
bundles remain accepted if their checksum matches. New exports include current
scope, historical-as-of availability, suite/story hierarchy, selected-range and
baseline re-execution totals, and per-result drill rows. The same frozen objects
feed Excel, PDF and JSON exports. A checksum detects mutation; it is not proof of
source authenticity, corporate validation or release approval.

DSR Center and TCR Center can reopen a saved report for the selected plan/kind.
Original dates and filters are shown separately from the current toolbar. Older
bundles can be imported through Audit History, then opened in the report center.
Frozen data and calculations are reproducible; binary file timestamps and future
renderer layout are not a guarantee of byte-for-byte file equivalence.

Tables across all modules support search, column filters, sorting, drill-down,
CSV, Excel and PDF. Exports include all matching rows, not just the visible page.
Excel allows 50,000 rows; table PDF allows 5,000 rows and 500,000 text characters;
requests retain the 20 MB limit. PDF uses the existing PDFsharp/font setup. No new
PDF library or corporate font deployment was added.

## Validation results

Final local checks:

| Check | Result |
| --- | --- |
| ASP.NET Core solution build (client built separately) | Passed; zero warnings/errors |
| Backend tests | 121 passed, zero failed/skipped |
| TypeScript / Vite production build | Passed |
| ESLint | Passed |
| Frontend unit tests | 14 passed |
| Playwright browser tests | 12 passed; includes all eight primary modules and incomplete DSR preview |
| Diff whitespace / tracked conflict-marker scan | Clean |

All fixtures are synthetic and confined to automated tests. No live ADO data,
corporate identity, AXA endpoint or installed extension was validated here.

Coverage includes dynamic additional plans, deep hierarchy and overlap, category
inheritance and subtree intersection, actual performer, Created By and exact
bug/result evidence, DST/leap-year month boundaries, all-module filter propagation,
re-execution/date/cancellation regressions, DSR/TCR freeze/reopen/import, and exports.

## Exact corporate VDI validation

After review, authorized commit/push and delivery, work from the repository folder
that contains Sandpiper.QA.Dashboard.slnx. Preserve any local changes before updating.

```powershell
git switch feature/sandpiper-command-center-v2
git pull --ff-only
git rev-parse HEAD
dotnet restore Sandpiper.QA.Dashboard.slnx -p:BuildClient=false
dotnet build Sandpiper.QA.Dashboard.slnx -p:BuildClient=false -p:ShouldRunNpmInstall=false
dotnet test tests/Sandpiper.QA.Dashboard.Server.Tests -p:BuildClient=false
Push-Location sandpiper.qa.dashboard.client
npm ci
npm run build
npm run lint
npm test
npm run test:e2e
Pop-Location
```

Prerequisites are the existing .NET 10/Node development tools, approved package
access, and Playwright Chromium for the synthetic browser suite. Passing those
tests is not live ADO validation. Do not bypass corporate proxy or endpoint policy.

1. Configure the existing local ADO organization/project and approved identity
   through the established development settings. Add approved SuiteCategories
   entries using actual plan/suite IDs. Rebuild/restart after configuration changes.
2. Open the .slnx in Visual Studio, sign in with the authorized organizational
   identity, select the existing http/https backend launch profile and start it.
   The local API uses port 5243 and client proxy uses 64423. Alternatively run
   `dotnet run --project Sandpiper.QA.Dashboard.Server --launch-profile http` and
   open `http://localhost:64423` once the existing SPA proxy starts the client.
3. Verify dynamically listed plans include 819259 and 839718, plus another plan
   accessible to the identity. Compare suite IDs, parents and full child depth to
   ADO. Expand a story-linked suite and verify its source requirement link.
4. Select a configured parent and Targeted Regression. Compare child inclusion,
   child-rule overrides and overlapping points to ADO; verify one point is counted
   once. Repeat with a sibling and another plan. Do not rely on folder names.
5. Select each outcome and tester. Navigate all eight modules. Confirm selectors,
   backend requests, source totals and frozen report metadata share that scope.
   Execution must follow actual performer; bug creation must follow Created By.
6. Verify Last Month/Last 2 Months request boundaries against UK local cutoff and
   DST. Select Custom Date and an approved earlier baseline; inspect distinct
   baseline and period counts. Do not infer historical membership from current
   suite selection. Re-execution diagnostics must explain exclusions/incompleteness.
7. Generate DSR and TCR drafts. Download Excel/PDF/evidence JSON. Reopen the saved
   draft and compare rows and metrics without additional ADO source requests.
   Restart the local process, import the JSON through Audit History, reopen it and
   confirm it is labelled imported/untrusted rather than newly authenticated ADO.
8. Filter a table and export Excel/PDF. Verify all matching rows (including rows
   beyond page 1), source identifiers, Unicode text and dates. Confirm no workbook
   formulas arise from untrusted title/text values.
9. Exercise missing history, permission failure and a date change during slow
   retrieval. Expect unavailable/incomplete/cancelled states, never verified zeros
   or stale scope totals. Use Refresh after new executions to clear local caches.
10. Record delivered commit SHA, source capture time, selected scope, expected ADO
    comparisons and sanitized aggregate diagnostics. These are the corporate
    reconciliation evidence; never paste access tokens or confidential payloads.

## Remaining limitations

Pre-commit review covered the complete 41-file change set. Credential/private-key/
token-pattern scans found no suspicious secret matches; source/test/doc review
found no confidential corporate payloads or fabricated production KPI values.
KpiCalculator.cs is unchanged from the base commit. The approved point/result
identity, sentinel-date, deduplication and re-execution fixes remain in place.
The DSR/TCR preview now withholds raw repeat-event zeroes on incomplete evidence,
matching the cards and exported summary. Distinct-case baseline labels remain
explicitly separate from the ADO point-based KPI.

The original unchanged TestAnalytics/configuration compatibility endpoint still
contains fixed example plan/suite IDs. Those examples are not called by V2 analytics,
which dynamically discovers plans and accepts arbitrary valid plan/suite IDs.
They were preserved to avoid unrelated legacy compatibility changes.

- No approved durable corporate snapshot database or automated historical cutoff
  capture exists. Frozen drafts are retained locally in memory for 60 minutes /
  25 entries; preserve JSON in approved storage. Missing past cutoff snapshots
  cannot be recreated authoritatively from current APIs.
- Retained result APIs cannot reconstruct overwritten attempts, deleted-point
  history or undisclosed permission/retention omissions. Current membership is
  disclosed; unknown identity/date evidence withholds complete affected totals.
- Category mappings must be approved/configured in the VDI; none are fabricated
  for the named plans. Source requirement IDs do not establish story acceptance.
- The current backend retains its local-development security boundary and fails
  closed for unapproved production use. Enterprise hosting/authentication and
  installed-extension backend connectivity await approved-environment validation.
- No email distribution, release sign-off, main merge, deployment or extension
  publication was performed. No credentials or confidential test data were added.

## Changed-file inventory

The following 41 files differ from the base commit, including the previous
uncommitted re-execution work preserved in this delivery:

- `AGENTS.md`
- `Sandpiper.QA.Dashboard.Server/Models/Analytics.cs`
- `Sandpiper.QA.Dashboard.Server/Models/ExecutionIntelligence.cs`
- `Sandpiper.QA.Dashboard.Server/Models/NormalizedPoint.cs`
- `Sandpiper.QA.Dashboard.Server/Models/ReportSnapshot.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ReportingClock.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ScopeProjector.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/TestPointNormalizer.cs`
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsClient.cs`
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsOptions.cs`
- `Sandpiper.QA.Dashboard.Server/Services/ExecutionHistoryService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/FrontierAnalyticsServices.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportExporter.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkspaceEvidenceService.cs`
- `sandpiper.qa.dashboard.client/e2e/command-center.spec.cjs`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/app/ReportingContext.ts`
- `sandpiper.qa.dashboard.client/src/components/ActivityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/DataTable.tsx`
- `sandpiper.qa.dashboard.client/src/components/OperationsPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/OverviewKpis.tsx`
- `sandpiper.qa.dashboard.client/src/components/QualityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/ReportPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/TableExportContext.ts`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `sandpiper.qa.dashboard.client/tests/backend-client.test.cjs`
- `sandpiper.qa.dashboard.client/tests/reporting-context.test.cjs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/AdoIntegrationTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ReportWorkflowTests.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionPointResolver.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ReexecutionCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/SuiteCategoryClassifier.cs`
- `docs/reexecution-reporting.md`
- `docs/unified-reporting.md`
- `sandpiper.qa.dashboard.client/src/components/ReexecutionDetails.tsx`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/AutomaticHistoryTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ReexecutionReportingTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/UnifiedReportingTests.cs`
