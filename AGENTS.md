# Sandpiper QA Command Center

## Purpose and business objective

Sandpiper QA Command Center is an enterprise QA analytics platform integrating
with Azure DevOps. Replace manual Excel Test Case Management (TCM), productivity
tracking, and repetitive Daily Status Reports (DSR) and Test Closure Reports (TCR)
with reliable, automated Azure DevOps reporting.

Data accuracy, security, performance, and maintainability are mandatory.

## Technology and deployment

- Frontend: React, TypeScript, Vite.
- Backend: ASP.NET Core Web API.
- Data source: Azure DevOps REST APIs.
- Deployment targets: local Visual Studio development and Azure DevOps extension.
- Preserve both deployment modes. An extension-only client does not satisfy the
  complete application requirements.
- Hosting is pending AXA IT/Cloud Architecture approval. Continue development with
  the existing local ASP.NET Core backend. Azure App Service and an approved managed
  reporting database are future preferences, not provisioned or approved resources.
- Do not assume database access, service identities, or Azure permissions exist.
  Keep hosting configuration environment-specific. Production builds must not
  hardcode localhost API URLs. Preserve installed ADO extension compatibility;
  do not claim its backend connection works until reachability and authentication
  have been tested in the enterprise environment.

## Test plans

- Frontier AI: 819259.
- Frontier AI – R2: 839718.
- Discover and support additional test plans dynamically within the authorized
  organization/project. These two plans are initial supported plans, not a
  hardcoded production allowlist.

## Mandatory engineering requirements

1. Preserve existing working functionality.
2. Inspect existing code before making changes.
3. Never introduce fabricated or hardcoded production KPI values. Do not silently
   substitute demo data or zeroes when live requests fail or data is incomplete.
4. Maintain correct test suite hierarchy and aggregation without double counting.
   Distinguish test-case, test-point, and execution identities explicitly.
5. Ensure KPI definitions align with agreed QA reporting rules. Document numerator,
   denominator, included/excluded outcomes, time window, timezone, and scope.
   Previously proposed formulas are not business-approved by default.
6. Optimize Azure DevOps API calls using safe concurrency, complete pagination,
   cancellation, bounded retries and caching where appropriate. Never present
   incomplete pages as complete totals.
7. Preserve authentication and enterprise security boundaries. Authenticate callers,
   enforce authorization, and scope caches and exports to permitted data. CORS is
   not authorization. Do not assume an extension token authenticates an arbitrary API.
8. Support standalone local development and ADO extension deployment. Keep the
   shared reporting behavior consistent across both modes.
9. Add meaningful error handling, logging, and automated tests. Do not log tokens,
   credentials, confidential test payloads, or unnecessary personal information.
10. Avoid unnecessary dependencies and large architectural rewrites. Retain the
    ASP.NET Core backend and original solution structure unless the user explicitly
    approves a concrete architectural change after reviewing its implications.
11. Never commit credentials, PATs, access tokens, or confidential test data. Keep
    generated outputs, local secrets, and dependency directories out of source control.
12. Document assumptions and identify unverified behavior. Distinguish absent,
    unavailable, stale, partial, and genuinely zero data.

## Development workflow

Before implementing a feature:

1. Analyze existing architecture and dependencies, including the original solution
   when recovering functionality missing from the current repository.
2. Identify affected files and compatibility implications for both deployment modes.
3. Explain the proposed changes and any business rules requiring clarification.
4. Implement the smallest safe solution within the authorized scope.
5. Run available build, lint, and tests appropriate to the change. Use synthetic
   fixtures only for tests; never display them as production reporting data.
6. Report changed files, test results, and remaining limitations. Identify checks
   that could not run and why.

Do not claim that live ADO behavior has been validated unless it has actually been
tested against the environment. Compilation, mocked API tests, and VSIX packaging
do not establish successful live authentication, permissions, pagination or reporting.

During design discussion, do not implement application changes or push commits
unless authorized. Preserve the original source before restructuring it, and make
omitted or replaced functionality explicit.

## Priority features

1. Accurate live execution analytics.
2. Fast, reliable test suite hierarchy.
3. TCR and DSR reporting.
4. Test re-execution and historical trends.
5. Defect and release-risk analytics.
6. Scrum Master and leadership dashboards.
7. Enterprise-ready extension integration.

## Reporting integrity

- Use one authoritative reporting contract for the frontend, backend, exports and
  scheduled reporting. Avoid separate conflicting KPI implementations.
- A latest test-point outcome is not execution history. Do not infer re-execution
  counts, daily productivity, or past-day state from current outcomes alone.
- A test plan is not automatically a release or time window. Establish explicit
  release scope, cutoff, timezone, baseline/reset and retention assumptions.
- Preserve drill-through evidence and show retrieval/cutoff times and completeness.
- Defect relationships, risk thresholds, productivity definitions, report templates
  and delivery recipients require documented business rules.
- Never send DSR/TCR messages or publish reports to others without user authorization.

## Agreed reporting rules

These user-confirmed rules supersede earlier proposals and conflicting existing code.
See `docs/reporting-rules.md` for implementation details and unresolved decisions.

- Reporting timezone: Europe/London, respecting UK daylight-saving transitions.
- DSR cutoff: 18:00 UK local time, configurable.
- DSR period: previous cutoff to current cutoff.
- TCR period: release start to release closure.
- Release scope: selected ADO Test Plan / Release, with explicit release boundaries.
- Suite scope: all suites by default; support selected-subtree filtering.
- Blocked points: count as executed and report separately.
- Not Applicable: exclude from applicable scope and report separately.
- Pending points: not executed.
- Re-executed points: track separately without increasing unique point totals.
- Primary pass rate: Passed / Executed, with blocked and N/A included in Executed.
- Secondary pass rate: Passed / (Passed + Failed + Blocked), excluding N/A and pending.
- N/A remains excluded from applicable progress scope; do not use the primary
  pass-rate denominator as the execution-progress numerator.
- Blocked result records qualify for re-execution only as distinct recorded attempts.
  A point is re-executed after more than one qualifying attempt in the selected scope.
- Store event/cutoff instants in UTC and convert using the reporting timezone.
  Do not replace Europe/London with a fixed UTC offset or the developer's timezone.
- Use the shared backend KpiCalculator; do not reintroduce frontend metric formulas.

## Current repository status

The user has authorized V2 Gates A–F sequential implementation and meaningful commits
and pushes on a dedicated feature branch. Separate design approval per gate is no
longer required. Do not merge main, deploy, publish extensions or provision corporate
infrastructure. Preserve explicit awaiting-validation states wherever access is absent.
No mandatory release mapping: use plan/suite selection and explicit baseline dates.

The local working tree restores the original ASP.NET Core backend, .slnx and .esproj,
with the React client in its original project folder. Push changes only when the user
explicitly authorizes publication. Backend calculations now serve the UI and the
original Frontier routes. Both initial plans and additional plans are supported.

History requires a selected offset-aware [from, to) window and current plan/subtree
membership. It is retained result history, not a complete retry/deleted-point audit.
V2 modules, retained execution/productivity analysis, result-linked quality evidence,
DSR/TCR draft generation, portable frozen evidence, exports, local caching and bounded
request audit metadata are implemented on the feature branch. See docs/v2-handoff.md
and docs/v2-gates.md for exact coverage and limitations. Authoritative historical cutoff
storage and scheduled distribution remain unavailable; no approved SQL infrastructure
or corporate authentication is assumed. The installed extension needs an approved
authenticated API; the current backend fails closed in production. All corporate
reconciliation and extension connectivity remain awaiting VDI validation.
