# Reporting contract — agreed requirements and design decisions

Status: shared backend calculation rules implemented locally on 19 October 2026;
live ADO behavior is not validated. These user-confirmed rules supersede earlier proposals.

## Confirmed by the user

| Setting | Agreed rule |
| --- | --- |
| Reporting timezone | Europe/London (UK local time) |
| DSR cutoff | 18:00 UK time; configurable |
| DSR period | Previous cutoff to current cutoff |
| TCR period | Release start to release closure |
| Release scope | Selected ADO Test Plan / Release |
| Suite scope | All suites by default, with subtree filtering |
| Blocked | Executed, with a separate blocked count |
| Not Applicable | Excluded from applicable scope, with a separate N/A count |
| Pending | Not executed |
| Re-execution | Report separately; never increase the distinct point total |
| Primary pass rate | Passed / Executed; blocked and N/A are included in Executed |
| Secondary pass rate | Passed / (Passed + Failed + Blocked); excludes N/A and pending |
| Blocked attempts | Qualify only as distinct completed, recorded execution attempts |

The meaning of 'executed' for reporting includes blocked points by business
agreement. It must not be confused with proof that a test action was physically run.

## Time and scope design

- Persist event times and report boundaries as UTC instants, retaining Europe/London
  and the configured local cutoff in report metadata.
- Resolve each day's cutoff independently in Europe/London. Do not subtract a fixed
  24 hours: a UK daylight-saving transition can make a reporting day 23 or 25 hours.
- Proposed boundary convention: include the previous cutoff and exclude the current
  cutoff, `[start, end)`, to avoid counting boundary events twice. This is an explicit
  implementation proposal; exact release-closure boundary handling remains to agree.
- A plan identifies scope, but does not supply an agreed release start/closure by
  itself. Configure the mapping and validate dates; do not infer missing boundaries.
- Default to every suite in scope. A subtree includes the selected suite and its
  descendants, deduplicating by point identity within the authorized org/project/plan.
- Never sum parent descendant totals together with child totals for the plan total.
- Distinguish current scope from historical scope. Deleted/moved points and scope
  changes need a documented baseline/history policy before final TCR reporting.
- Keep daily execution activity separate from the latest point status at cutoff.
  Reconstruct cutoff state only where historical evidence supports it.

## KPI policy still requiring definition

- Complete classification for timeout, error, aborted, inconclusive, paused,
  in-progress, unspecified, missing and unfamiliar outcomes.
- Retry identity and handling of updated results, iterations, resets and copied points.
- Release start/closure source, reopen behavior and scope baseline/change policy.
- Calendar-day versus working-day DSR scheduling and exact boundary conventions.
- Defect relationship types, states/severities, risk rules and report exit criteria.
- Productivity attribution, non-execution work, and appropriate access to individual data.

The approved portions constrain the future implementation: applicable scope excludes
N/A; executed includes blocked; pending is excluded. Unknown outcome policies must
not be filled in silently to manufacture a complete execution percentage.

## Hosting and enterprise access

- Production hosting is pending AXA IT/Cloud Architecture approval.
- Continue with the existing local ASP.NET Core backend and local Visual Studio flow.
- Prefer Azure App Service for a future hosted API and an approved managed database
  for history, subject to architecture and security approval.
- No production database, identity, infrastructure or network permission is assumed.
- Do not provision infrastructure or introduce production service credentials.
- Keep API endpoints, origins, identity configuration and storage settings specific
  to each environment. Do not compile localhost URLs into production extensions.
- The installed extension needs an approved, reachable API with verified caller
  authentication. A local development connection is a separate environment proof;
  preserving the extension package alone does not validate enterprise deployment.
- Until historical storage is available and approved, label unavailable historical
  reporting honestly. Do not fabricate prior snapshots or store confidential test
  data in Git as a substitute.

## Implementation impact

The local working tree restores the original ASP.NET solution and client project
folder. `Services/Analytics/KpiCalculator.cs` owns metric calculation; the frontend
displays its values without recalculation. Rule version: `2026-10-19.1`.

Current explicit policy for less common outcomes retains terminal inconclusive,
timeout, aborted and error outcomes in Executed, each visible separately. Paused and
in-progress are not executed. None, active and notExecuted are pending. Missing,
unspecified, warning, notImpacted and unfamiliar outcomes are unknown/unclassified
for completion; the original raw category counts remain visible. This policy is
documented, not a claim of verified equality with every ADO chart.

- Executed = passed + failed + blocked + N/A + other terminal outcomes above.
- Applicable = distinct total - N/A.
- ExecutedApplicable = Executed - N/A.
- Progress = ExecutedApplicable / Applicable.
- Primary and secondary pass rates follow the agreed formulas above.
- Zero denominators return null and the UI displays N/A.
- Duplicate points with matching outcomes count once. Conflicting duplicate
  outcomes fail the snapshot rather than silently choosing a result.
- Re-execution counts distinct (runId, resultId) identities in Completed state,
  with a qualifying terminal outcome, a selected point link and completedDate in
  the selected window. Blocked qualifies; N/A does not establish an attempt.
  Iterations and steps are not additional attempts. Missing dates or links withhold
  the complete re-execution count. Conflicting duplicate result identities fail.
- History is filtered by result completion time, not the run's start time.
- Current scope membership is used. Baseline/deleted-point reconstruction and a
  fully persisted DSR/TCR reporting history are still pending.

## Required validation before release

Test blocked/N/A/pending behavior, duplicate points, subtree rollups, pagination,
UK summer/winter cutoffs, boundary events, release limits, scope changes, retries,
authorization, missing history and failed/partial collections. Validate local and
installed-extension operation separately against the actual authorized environment.

## Case-based business KPIs — 20 October 2026

Total Test Cases counts distinct mapped ADO Test Case IDs in the selected current
scope. Total Test Points remains distinct point IDs. Executed Test Cases has at
least one current point in the existing executed-status set, including Blocked/N/A.
Case Execution Coverage = Executed Test Cases / Total Test Cases. Point Execution
Progress retains its applicable denominator excluding N/A. Empty denominators are
unavailable. Missing case mappings or unclassified execution evidence withhold the
affected complete totals; known mapped identities remain drillable.

Passed/Failed/Blocked/Pending/N/A and pass rates remain point-based. No mixed-case
configuration outcome policy is invented. Re-executed Test Cases counts distinct
case IDs with at least one point having two qualifying baseline attempts. Multiple
repeated configurations count the case once. Technical points/repeat events remain.

Total/Open/Completed Defects use inspected selected-case relationships and recorded
result Bug links, deduplicated by Bug ID. Complete totals require reconciled history,
explicit relation arrays and supported metadata. Unknown categories withhold open/
completed counts. No project-wide Bug inference or automatic verification is added.
Current tester filtering uses assignment; history uses actual performer; defect
totals cover selected plan/subtree across testers, while daily Bug activity uses actor.

See kpi-ui-enhancement.md for exact definitions, zero-path findings and VDI checks.
