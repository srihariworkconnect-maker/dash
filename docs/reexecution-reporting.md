# Selected-range re-execution reporting

## Contract (21 October 2026)

The requested ADO Progress Report label **Re-executed Test Cases** means distinct
test **points** having at least two qualifying recorded attempts in the selected
reporting range. Two repeated configurations/points mapped to one case count as
two. This supersedes the earlier distinct-case label in Overview; the older
`caseRetesting` API field retains its original distinct-case semantics.

**Total Re-execution Attempts** = sum(execution count - 1) across repeated points.
Both complete totals and drill rows come from `activity.reexecution`. Missing
timestamps, point links or unreconciled current-result evidence withhold complete
totals; known rows remain available. No frontend recomputation or demo fallback.

Qualifying attempts retain the existing policy: persisted Completed results with
Passed, Failed, Blocked, Timeout, Error, Aborted or Inconclusive outcomes. N/A,
pending and in-progress results do not establish attempts. Previous outcomes do
not gate repetition. Identity is (run ID, result ID); duplicate identities count
once, conflicting records fail. Equal timestamps still represent distinct result
identities but do not establish transition chronology.

Scope is [From inclusive, To exclusive), offset-aware instants. Custom Range uses
the entire displayed range while preserving an explicitly selected repeat/release
baseline. Selected-range totals do not borrow older baseline attempts. Release-to-Date
requires a user-confirmed authoritative release start and nonblank source/reference;
it never copies an arbitrary baseline or assumes a plan creation date is a release.
Changing plans clears release confirmation. Editing the release start/reference
also clears confirmation and history scope. The end is the displayed cutoff;
extend it explicitly to include later executions. UK reporting uses Europe/London.

The reference is user attestation in the current browser session, not an automated
corporate release-system lookup or persisted release registry. It is not added to
existing frozen report exports. No approved release registry is provisioned.

Other legacy baseline-based fields, productivity, DSR/TCR calculations and current
point KPIs are preserved. Tester filtering counts only that executor's attempts
in the new report, rather than treating another tester's first attempt as theirs.

Year-one sentinel dates are normalized to null both on current points and history
results; real offset-aware dates preserve their UTC instant. Result IDs and
Completed states still prevent unspecified outcomes being misclassified Pending.

## VDI validation

1. Pull the approved feature-branch delivery, launch the existing local ASP.NET Core
   backend and client, and sign in using the approved Visual Studio ADO identity.
2. For plans 819259 and 839718, compare a known multi-run sample to retained ADO
   results. Verify run/result IDs, actual executor, outcomes and timestamps.
3. Choose Custom Range, including one attempt exactly at From and excluding one
   at To. Click Re-executed Test Cases; inspect both points for a shared case ID.
4. Search/filter/sort rows, select a point, and reconcile each attempt plus the
   additional-attempt total. Check Passed→Passed, Failed→Passed and Blocked→Passed.
5. Select Release-to-Date. Confirm the approved start and source/reference. Verify
   no release history loads before confirmation and switching plans clears it.
6. Change dates during a slow load; verify cancellation and absence of stale totals.
7. Compare empty, inaccessible and incomplete history separately. Missing evidence
   must not display a complete zero.

Automated checks use synthetic fixtures, not live corporate data. History is
retained result-level data scoped to current plan/subtree membership. Overwritten
attempts within the same result identity, deleted points, retention gaps and API
permission omissions cannot be reconstructed. VDI reconciliation and installed
extension connectivity remain awaiting approved-environment validation.

## Implementation verification and changed files

Working branch: `feature/sandpiper-command-center-v2`, fetched and fast-forwarded
from `b58de6f` to `dd3df30` before implementation. Existing token-cache and
organization-scoped WIT fixes are present, including singleton token-cache wiring
and their automated tests. The subsequent unified delivery was conditionally
authorized for feature-branch commit/push after verification on 21 October 2026.
See unified-reporting.md and Git history for delivery status and final checks.

Checks performed locally:

- ASP.NET Core solution build: passed, zero warnings/errors (client built separately).
- Backend tests: 112 passed, none failed/skipped.
- Frontend TypeScript/Vite production build and ESLint: passed.
- Frontend unit tests: 13 passed.
- Playwright browser tests: 10 passed, including obsolete-request cancellation.
- Tracked-file conflict-marker scan and diff whitespace check: clean.
- No live Azure DevOps or installed-extension integration validation performed.

| File | Change |
| --- | --- |
| `AGENTS.md` | Record the newly approved terminology and approval-before-commit requirement |
| `Sandpiper.QA.Dashboard.Server/Models/ExecutionIntelligence.cs` | Add selected-range report and point-detail contracts; omit null report for older bundles |
| `Sandpiper.QA.Dashboard.Server/Services/Analytics/ReexecutionCalculator.cs` | Selected-range counting, identity deduplication, completeness checks and detail rows |
| `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionCalculator.cs` | Attach additive report without repurposing legacy fields |
| `Sandpiper.QA.Dashboard.Server/Services/Analytics/TestPointNormalizer.cs` | Sentinel-date normalization |
| `Sandpiper.QA.Dashboard.Server/Services/ExecutionHistoryService.cs` | Apply same sentinel handling to result timestamps |
| `sandpiper.qa.dashboard.client/src/services/adoApi.ts` | Typed additive API contract |
| `sandpiper.qa.dashboard.client/src/app/ReportingContext.ts` | Explicit release confirmation and plan reset |
| `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx` | Release source/date controls and full custom-range selection |
| `sandpiper.qa.dashboard.client/src/components/OverviewKpis.tsx` | Point-based ADO label, additional-attempt card and clickable detail |
| `sandpiper.qa.dashboard.client/src/components/ReexecutionDetails.tsx` | Filterable/sortable point and attempt tables with empty/incomplete states |
| `sandpiper.qa.dashboard.client/src/components/ActivityPanel.tsx` | Reuse drill-down in Execution Intelligence |
| `tests/Sandpiper.QA.Dashboard.Server.Tests/ReexecutionReportingTests.cs` | Outcome sequences, dates, duplicate IDs, shared cases, sentinel and attribution regressions |
| `tests/Sandpiper.QA.Dashboard.Server.Tests/AdoIntegrationTests.cs` | Overlapping/conflicting result pagination and diagnostic-log confidentiality fixtures |
| `sandpiper.qa.dashboard.client/tests/reporting-context.test.cjs` | Release confirmation/reset tests |
| `sandpiper.qa.dashboard.client/e2e/command-center.spec.cjs` | Updated point semantics and release, drill-down, empty-state, cancellation checks |
| `docs/reexecution-reporting.md` | Calculation contract, delivery results, limitations and VDI instructions |

## Automatic-history investigation and verified corrections

No normal KPI workflow requires users to enter point or run/result IDs. The backend
discovers plan-linked runs, reads paginated results and maps them automatically.
The VDI's exact data-specific cause remains unverified because corporate history
is unavailable here. Two reproducible false-zero paths were identified in code:

- A positive result point ID absent from current plan membership was discarded
  before completeness checks. A pending current-point snapshot could then return
  a verified zero. The full-plan resolver now distinguishes unknown identities
  from legitimate selected-subtree exclusions, and unknown qualifying identities
  withhold complete totals. Regression: `UnknownSourcePointCannotSilentlyBecomeVerifiedZero`.
- The legacy Frontier history endpoint parsed year-one sentinels as real dates,
  excluded them as old results and could report complete zero. It now uses the
  shared null sentinel normalizer. Regression: `LegacyEndpointTreatsSentinelAsMissingHistoryRatherThanZero`.

Missing testPoint references were already flagged incomplete, but could not be
resolved automatically. The resolver now uses exact (case ID, suite ID,
configuration ID) metadata when all three are present and yield exactly one
point. It never uses a case-only guess, replaces an unknown positive point ID,
or merges distinct configurations. Contradictory direct/reference metadata and
ambiguous identities remain incomplete.

Deduplication uses (run ID, result ID) throughout calculation, independently of
transport keys. Conflicting state/identity evidence fails explicitly. Existing
pagination continues until an empty page, deduplicates overlapping records, and
fails on changed or stalled history. Transport and permission failures propagate
as unavailable/error states, not successful zero reports.

`activity.reexecution` covers the selected activity range.
`activity.baselineReexecution` covers [baselineFrom, activity.To).
The existing `repeatEvents` field remains period repeat activity, where a period
attempt can be a repeat of an earlier baseline attempt. These are visibly labelled
separately. Example: one attempt before From and one inside From/To gives baseline
1 repeated point/1 additional attempt, selected range 0/0, period repeat activity 1.

Diagnostics are count-only: source result records, unique run/result identities,
duplicates, qualifying executions, unique points, repeated points, additional
attempts, mapping status and mutually exclusive exclusion reasons. Reasons include
NotCompleted, NonQualifyingOutcome, OutsideSelectedPoints, MissingOrSentinelDate,
BeforeStart, AtOrAfterEnd, UnknownPointIdentity, missing/ambiguous/conflicting
identity and DifferentExecutor. UniqueResults = qualifying executions + excluded
records. SourceResults = UniqueResults + DuplicatesRemoved. Calculation source
counts are after transport-page deduplication; separate pagination logs expose
raw page records, unique IDs, duplicate rows and pages. No tokens, names, test
titles or result payloads are added to diagnostic logs. Existing upstream error
body logging was removed because response bodies can contain confidential context.

Additional changed files in this correction:

| File | Change |
| --- | --- |
| `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionPointResolver.cs` | Full-plan direct and exact identity mapping |
| `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsClient.cs` | Pagination aggregate counts; remove upstream error-body logging |
| `Sandpiper.QA.Dashboard.Server/Services/FrontierAnalyticsServices.cs` | Legacy history sentinel normalization |
| `Sandpiper.QA.Dashboard.Server/Services/WorkspaceEvidenceService.cs` | Aggregate calculation diagnostics for automatic Overview evidence loads |
| `tests/Sandpiper.QA.Dashboard.Server.Tests/AutomaticHistoryTests.cs` | Nine automatic retrieval/mapping, false-zero, scope, diagnostics and API-failure regressions |

The model, history service, calculators, UI contracts, drill-down, global date
controls and browser fixtures listed above also include the corresponding
diagnostic and baseline/period changes. No dependency changes or infrastructure.

### VDI checks without manually supplied IDs

After approved delivery and rebuilding both backend/client:

1. Start the existing local backend under the approved Visual Studio ADO identity.
   Select plan 819259, all suites, all testers and an explicit reporting range.
   History loads automatically. Repeat with plan 839718 and any additional plan.
2. Open the Re-executed Test Cases card and expand Execution diagnostics. Review
   qualifying executions, unique points, repeated points and additional attempts;
   reconcile aggregate exclusions using the count equations above.
3. Select a suite parent; child-suite points remain included, sibling points become
   OutsideSelectedPoints. Configurations of the same case remain separate points.
4. Compare selected range and baseline totals using an approved earlier baseline.
   A single in-period attempt may be repeat activity, without being two executions
   inside the selected range. Custom Range must preserve the selected baseline.
5. On incomplete data, inspect mapping/date reasons; on authentication/API failure,
   expect unavailable/permission feedback. Neither should produce a complete zero.
6. Use Refresh to clear scoped caches after new executions; verify pagination and
   aggregate calculation logs. Confirm old and new builds are not running together.

This establishes checks to perform, not a claim they passed against corporate ADO.
Repeated updates to the same retained run/result identity are not automatically
distinct qualifying attempts; no revision count, elapsed time or latest point
status is converted into fabricated execution history.
