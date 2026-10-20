# V2 gate validation ledger

All gates are authorized sequentially on feature/sandpiper-command-center-v2.
No default-branch merge, deployment, extension publication, or corporate provisioning is authorized.

## Gate A — data foundation

Preserved original routes and KPI formulas. Added normalized point/case/configuration
identities, memberships, assigned and actual performer references, latest-result
references and raw outcome/type evidence. Unspecified is never guessed as Pending.
Collection completeness and corporate reconciliation are separate states.

Tests: normalization fixtures plus existing KPI, scope, pagination and route tests.
VDI: authenticate in Visual Studio, compare both plans with identical Progress Report
suite/configuration/active scope and retrieval time. Inspect raw types and latest run/
result IDs for R2 unknowns. Do not send confidential raw payloads to Git or logs.

Limitation: the approximately 761 unspecified R2 points are user-reported, not
reproduced here. Live reconciliation remains awaiting corporate validation. Downstream
features may develop against normalized contracts but must retain this warning.

## Gate B — command center and current-state TCM

Added collapsible eight-module navigation, sticky global reporting filters and scope
breadcrumbs, current-state KPI drill-through, clickable status distribution,
own/subtree suite metrics, searchable Explorer and read-only point-based TCM.
Tables support global and column search, sorting, column visibility, metadata-only
saved views, pagination and source-record detail. Case links use the backend source URL.
Plan changes clear incompatible suite/tester selections. Unknown enrichment is unavailable.

Tests: backend regressions, TypeScript build, ESLint and eight client contract/context
tests. VDI: verify plan/suite/tester selection, status drill-through, metadata-only
saved views, large plans, links, keyboard navigation and no stale scope after switching.
Limitations: corporate data unvalidated; historical and work-item columns await C/E.
Today/Yesterday are UK cutoff days (may be in progress); Release-to-Date requires an
explicit start. No mandatory release mapping or fabricated metrics are introduced.

## Gate C — execution and productivity

Added normalized run/result events, actual performer identities, baseline-aware repeat
events, period activity, daily UK-day trends, status events, distinct point counts,
and chronological failed/passed transitions. Result-level bug IDs are retained as
source relationships, not interpreted as verified defects. Unattributed performers,
missing dates/links and equal-time chronology are explicit.

Tests: 30 backend tests, client build/lint and existing client regressions. VDI: use
a point executed before the selected day and again within it; choose a baseline
covering both. Confirm repeat event today, distinct point unchanged, performer differs
from assignee where appropriate. Compare result IDs and links in ADO.
Limitations: current assignments are not historical assignments; retained results
cannot recover overwritten retries/deleted points. Corporate integration unvalidated.

## Gate D — reproducible report drafts and outputs

Implemented shared DSR/TCR evidence drafts, narrative/exclusions, current-state vs
period labeling, daily productivity/trend sections, email-ready summary, XLSX and
Unicode PDF generation, immutable in-memory evidence snapshots and checksum-checked
portable JSON bundles. Reimport never establishes trusted provenance. No email/sign-off
is automatic. No corporate database/schema/infrastructure is provisioned.

Tests: 33 backend tests including workbook XML/formula safety, checksum preservation,
and PDF font/header checks; client build/lint. PDFsharp is MIT-licensed; its transitive
PKCS dependency is pinned to Microsoft's patched 10.0.13 release.
VDI: generate DSR and TCR with explicit period/baseline, compare workbook/PDF/preview,
download and preserve evidence in approved storage, restart and reimport evidence.
Limitations: store is local process memory, 60 minutes/25 reports, not approved SQL.
Past cutoff-state evidence is unavailable, so reports are drafts, never authoritative
historical DSRs. The R2 discrepancy remains unvalidated. Font availability needs VDI QA.

## Gate E — result-level defect and explainable quality evidence

Implemented runtime inspection of WIT fields, Bug state categories, requirement
categories and relation metadata; point-linked result bugs; revisions paged by rev,
not repeated work-item ID; Created By/changed actor activity; retest chronology;
candidate/verified/failed-again/reopened/missing-evidence queue; requirement links,
attachments and priority enrichment; open linked-defect scope; configurable readiness
indicators (disabled by default); explicit throughput scenario estimates and cross-plan
current-state comparison. Missing relationship collections withhold defect totals.

Verification requires linked failure, later resolved revision, later explicitly linked
pass, known performer, and subsequent same-performer completion revision, without
contradictory later evidence. A later pass alone stays a candidate. Equal-time evidence
does not establish verification chronology. All outputs remain corporate-unvalidated.

Tests: evaluator chronology/candidate/readiness cases, forbidden-schema handling,
runtime metadata inspection, revision identity and existing regressions. VDI: inspect
your actual Bug metadata, associate bugs at failed-result level, compare revision
actors/times and later results. Confirm unknown relationships remain unavailable.
Limitations: no live corporate Bug schema was accessible here. These rules are an
explicit implementation to validate, not a claim of confirmed workflow integration.
Forecast scenarios are assumptions, not AI or release approval. Blocker-start aging
cannot be recovered from current status alone; Gate F adds conservative observed-streak
evidence only when a current result can be reconciled to history.

## Gate F — performance, security and handoff

Implemented bounded identity/source-partitioned plan and revision-keyed run caches,
single-flight loads, refresh invalidation, current-assignment projections using the
shared calculator, bounded metadata audit including denied requests, no-store/nosniff
headers, import/export bounds and validated reporting/quality configuration. Reuses
one cached whole-plan hierarchy across subtree/tester projections. Conflicting point
configuration/latest-result metadata fails the collection instead of hiding a conflict.

Added virtual table scrolling, full-filter Excel/CSV exports, local frozen report
history/import UI, reference-count reconciliation, sign-in diagnostics, configured
cutoff display, richer point/history/bug/attachment details, own-suite repeat hotspots,
daily bug attribution and observed blocker-streak aging. Preserved extension SDK,
relative assets and production endpoint/authentication safeguards. Added Chromium
browser regression tests with 5,000 synthetic points confined to test interception.

Final checks: full .slnx build (zero warnings/errors), 51 backend tests, 9 client tests,
ESLint and frontend build. Three Chromium regressions exercise eight-module navigation,
scope reset/preservation, virtual rendering, export scope and refresh failure behavior.
Local VSIX packaging succeeds. Backend advisory scan reports none; the packaging UUID
override removes npm advisories while retaining the compatible tfx-cli version.

VDI: follow docs/v2-handoff.md in gate order. In particular, reconcile both Frontier
plans before accepting reports, test actual run/result and Bug relationships, confirm
Unicode/PDF output and large-table responsiveness, time cached/cold/refresh behavior,
and review audit metadata and denied-request behavior. Installed-extension connectivity
must be tested only with an approved endpoint and authentication arrangement.

Limitations: no corporate credentials, approved hosting/authentication, SQL database,
durable synchronization watermark, authoritative historical cutoff snapshots or
automated distribution. Local process caches/audit/report drafts are explicitly bounded
and volatile. Source/history/schema and installed-extension behavior remain awaiting
corporate validation. The API intentionally remains unavailable for production data.
