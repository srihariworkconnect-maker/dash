# Sandpiper QA Command Center V2 — implementation handoff

Implementation branch: `feature/sandpiper-command-center-v2`. Baseline: `7ccf470`.
This enhances the original .NET solution and React client; it does not replace them.
No default-branch merge, deployment, extension publication or corporate provisioning
is included. The user's latest instruction authorizes sequential Gates A–F.

## Implemented functionality

| Module | Delivered behavior |
|---|---|
| Overview | Backend current-state KPIs, status drill-through, suite progress, optional period/tester/defect/risk evidence |
| Test Explorer | Dynamic hierarchy, searchable subtree selection, own/subtree totals, test-case links, case details |
| Live TCM | Point/case/configuration identities, assignment vs performer, latest state, history counts, priority/requirements, result-linked defects and attachments, column views, search/filter/sort, Excel/CSV |
| Productivity | Actual performer/day events and distinct points; current assigned/pending roster including inactive testers; repeat events; daily Created By/revision/retest bug activity; team and evidence detail |
| Execution Intelligence | Explicit baseline and period, run/result deduplication, prior-day repeats, failed/passed transitions, own-suite repeat hotspots and history |
| DSR | Shared frozen evidence draft, current outcomes separated from period activity, daily tester/bug activity, carry-forward narrative, Excel/PDF/email-ready text |
| TCR | Explicit release dates/scope, outcomes, linked requirement gaps, re-execution/regression evidence, risks/exclusions/recommendation, Excel/PDF |
| Quality Intelligence | Runtime WIT schema/categories, result-linked defect queue and revision chronology, Created By attribution, observed blocker aging, regression evidence, linked requirement gaps, configurable readiness indicators, throughput scenarios and full-plan comparison |
| Supporting views | User-reference reconciliation, local sign-in check, frozen draft history/import, bounded metadata audit |

All tables support filtered exports, saved column/sort views and optional virtual
scrolling. Browser saved views contain layout metadata, not test records. Live TCM
requires explicit enrichment to load retained history and WIT evidence. No execution
status can be edited in this dashboard. ADO remains the source of truth.

## Shared rules and scope

- Primary pass = Passed / Executed, including Blocked and N/A in Executed.
- Secondary pass = Passed / (Passed + Failed + Blocked).
- Applicable execution progress excludes N/A from numerator and denominator.
- Every source outcome remains separately visible. Numeric/unknown/unspecified
  outcomes are preserved and not guessed to be pending.
- Qualifying re-execution attempts are distinct completed retained run/result records
  with terminal execution outcomes, including Blocked; N/A is a status, not an attempt.
- Baseline repeated points have two or more qualifying attempts. Period repeat events
  can refer to an earlier first attempt in the baseline. They do not increase unique points.
- Time windows are UTC instants `[from,to)`, displayed with Europe/London rules.
  Cutoff is server-configurable, default 18:00 UK. Assignment is current state;
  activity filtering uses actual performers. Missing performers remain unattributed.
- Case, point, configuration, suite, run and result identities remain distinct.
  Parent scope includes descendants; overlapping membership is deduplicated.
- Result-linked bugs are not all project bugs. Requirement-link gaps are selected
  cases without inspected requirement links, not a project-wide coverage percentage.
- Verified defects require explicit source relationships and chronology. A later
  pass alone is only a candidate. Readiness indicators never grant release approval.

See `reporting-rules.md` and backend calculation tests for the full contract.

## Performance and local security

ADO calls share bounded concurrency (default four), cancellation, pagination and
bounded 429/503 retries. Whole-plan snapshots are cached for 30 seconds by default
(configurable 0–60). Completed-run results can be reused for 60 seconds using run
revision/update keys; recent aggregated history is cached for 30 seconds. Refresh
invalidates local source caches. No persistent synchronization watermark is claimed.

Caches are in memory, bounded to 64 entries and partitioned by organization/project
and a non-exported hash of the acquired credential token. Different caller tokens
cannot share a source cache entry. This is a local single-user boundary, not a future
enterprise authorization design. Cache invalidation cannot restore an older in-flight
load to the cache. Failed/partial collections are never cached as zero totals.

The API still requires Development, loopback remote address/Host, an allowed Origin
when supplied, and the dashboard header. All production data routes fail closed.
Responses use no-store and nosniff; upstream error payloads and credentials are not
displayed. Local request audit retains only the latest 500 metadata entries, including
denials. It is neither tamper-resistant nor an enterprise security audit system.
Body/import and workbook limits bound local operations; downloads contain confidential
test evidence and must be saved only to approved storage.

## VDI validation sequence

1. Check out this feature branch. Install .NET 10 SDK, supported Node (22.12+/24 LTS),
   and the client lockfile dependencies. Open the existing `.slnx` in Visual Studio.
   Sign in with the authorized corporate account. Configure organization/project/tenant
   using ignored `appsettings.Local.json` or environment settings, never credentials.
2. Use the server's `http` profile and Vite at port 64423. Verify health through Vite,
   then test plan discovery. Token acquisition alone does not verify ADO permissions.
3. **Gate A acceptance first:** select Frontier AI 819259 and Frontier AI – R2 839718.
   Compare raw outcomes/types, point/case/configuration IDs, all pages, own/subtree
   counts, and latest result evidence with the same-scope ADO Progress Report.
   Capture timestamps and use Data Reconciliation to enter reference counts.
   Investigate the previously reported ~761 unspecified R2 points; never reclassify
   them merely to make totals match. The comparison does not independently certify data.
4. **Gate B:** navigate eight modules, search plans/suites/testers, select a parent
   suite and a leaf, inspect a case/configuration, check filters survive module changes
   and reset incompatible selections on plan change. Compare filtered Excel rows and
   enabled columns to TCM; try pagination/virtual scrolling with a large authorized scope.
5. **Gate C:** use real distinct result IDs for Passed/Failed/Blocked and a repeated
   result update. Confirm duplicate updates are not separate attempts. Put the first
   execution before the chosen day and the repeat inside it. Compare performer to
   assignee, unknown identities, suite hotspots and baseline/period counts.
6. **Gate D:** choose explicit period and baseline; generate DSR/TCR drafts. Compare
   preview, workbook and PDF against the same frozen evidence. Inspect Unicode,
   page wrapping and all worksheets. Export JSON to approved storage, restart and
   reimport; confirm the imported/untrusted label. Never treat collection-time state
   as a reconstructed past cutoff or publish a draft as an authoritative report.
7. **Gate E:** inspect the actual corporate Bug fields/state categories and failed
   test-result relationship. Validate creator/revision/retest actors and chronology
   with your workflow. Try a later pass without an explicit bug link: candidate only.
   Check failed-again/reopened/unknown, requirement links and evidence attachments.
   Validate blocker onset only where latest-result linkage matches. Approve readiness
   thresholds before enabling them; review forecast assumptions and scope.
8. **Gate F:** time cold/warm/refresh requests and filtering in VDI; confirm cached
   source timestamps, throttling/cancellation and error behavior. Check local audit
   excludes source payloads/credentials. Run automated checks. Have enterprise teams
   separately review the hosting, identity, authorization, retention and extension plan.

Run from repository root:

```powershell
npm --prefix sandpiper.qa.dashboard.client ci
npm run build
npm run lint
npm test
dotnet build Sandpiper.QA.Dashboard.slnx -p:BuildClient=false -p:ShouldRunNpmInstall=false
dotnet test tests/Sandpiper.QA.Dashboard.Server.Tests -p:BuildClient=false
npx --prefix sandpiper.qa.dashboard.client playwright install chromium
npm --prefix sandpiper.qa.dashboard.client run test:e2e
npm run package
```

Browser tests intercept HTTP only in the test process. They never install demo data
or bypass authentication in application code. VSIX creation is local packaging only.

## Pending enterprise work and evidence limitations

- Corporate reconciliation, the R2 discrepancy, Bug workflow conventions, API volumes,
  and Visual Studio interactive F5 still require the user's VDI.
- No approved API endpoint or corporate caller authentication is available. Installed
  extension connectivity has not been validated. The SDK contribution/relative assets
  are preserved; `VITE_API_BASE_URL` requires an approved HTTPS endpoint. Production
  localhost is rejected. An extension token is not accepted as arbitrary API identity.
- Reports are frozen local drafts with checksum-checked portable evidence, stored for
  60 minutes/up to 25 reports per process. Checksums are not signatures or provenance.
  Authoritative historical cutoff snapshots, deleted scope, overwritten retries and
  long-term trends cannot be recovered from current ADO state or volatile caches.
- Incremental reuse is process-local and revision keyed, not a durable background
  synchronization service. No scheduler or automatic distribution was provisioned.
- Forecasts are transparent throughput scenarios, not commitments or statistical
  confidence bounds. Unknown evidence withholds affected metrics.
- Blocker aging is an observed recorded streak or unknown/lower-bound result, not
  a reconstruction of every past status change. Missing original evidence stays unknown.
- PDF output uses Windows Arial; other hosting OS/font provision is unvalidated.
  Corporate Excel/PDF layout and actual large-source performance require manual QA.

## Future approved storage and hosting boundary

Subject to AXA approval, the preferred enterprise arrangement remains an authenticated
ASP.NET Core API on approved hosting (potentially App Service) and an approved SQL
Server/Azure SQL reporting store. No connection string, identity, database or schema
is created by this branch. Before implementing that layer, agree tenant/project/plan
authorization, delegated vs workload identity, encryption, retention, backup, audit
ownership, source permissions and the extension's endpoint/CORS/token flow.

A durable design should preserve source organization/project/plan, suite membership
snapshots, point/configuration identity, versioned run/result records, WIT revisions,
collection batches and completeness, synchronization watermarks and versioned frozen
report/evidence metadata. Use unique source keys and transactional batch completion;
never advance a watermark after a failed page. Historical snapshots must capture
cutoff state before advertising historical DSR reproducibility. Portable evidence
is the interim reproducibility mechanism, not a substitute for approved SQL retention.
