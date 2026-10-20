# V2 validation — 20 October 2026

## Passed

- Full restored `.slnx` build with `BuildClient=false` and
  `ShouldRunNpmInstall=false`: zero warnings/errors; frontend built separately.
- React TypeScript/Vite production build and ESLint.
- **51 backend tests**: KPI denominators/status visibility, distinct attempt identities,
  normalization/raw types and conflicting point metadata, UK DST/cutoffs, pagination,
  dynamic plans and original Frontier routes, scope projection, single-flight/cache
  invalidation/isolation, API protection/production fail-closed, frozen checksums,
  workbook formula safety, Windows PDF/font output, chronological defect verification,
  schema failure handling, daily bug actors and conservative blocker aging.
- **9 client tests**: endpoint constraints, request header/cancellation, mismatched scope,
  HTML rejection, period forwarding, hierarchy, plan-filter reset, TCM identity and
  denominator drill-through record selection.
- **3 Chromium browser tests**, test-only HTTP interception: eight-module navigation,
  shared context preservation/reset, 5,000-point virtual rendering, filtered workbook
  request/download, detail access, source refresh invalidation and failures without
  fabricated totals. Workbook binary correctness is tested separately in backend tests.
- Local VSIX creation. This is not publication or installed-extension connectivity.
- Dependency scans: no backend vulnerable packages reported; zero npm advisories after
  targeted `tfx-cli -> uuid 11.1.1` override. A tested newer tfx-cli pulled an unpatched
  braces chain, so the compatible tfx-cli 0.22.1 is retained with the UUID override.
  Some development dependencies are deprecated; advisory-free does not guarantee safety.
- Actual local HTTP smoke check, without mocked ADO data: direct API health and Vite
  proxied health returned 200, proxied reporting configuration returned Europe/London /
  18:00 / 30-second cache, missing protection header returned 403, and explicit cache
  refresh returned 200. Temporary servers were stopped afterward.

## Unsuccessful external validation

A real read-only ADO plan-discovery request through Vite/API returned **503**. Server
classification was `CredentialUnavailableException`. No live ADO payload was displayed
or stored. No corporate plan totals, R2 unspecified discrepancy, actual Bug conventions
or installed-extension API connectivity have been validated here.

Tests use controlled synthetic fixtures only inside test projects/browser interception.
Neither passing fixtures nor packaging establishes corporate integration. Visual Studio
interactive F5 was not exercised; CLI startup and actual Vite/API proxy were exercised.
The workspace-local .NET SDK and downloaded browser are outside source control.

## Remaining limitations

The implementation of all eight modules and supporting gate features is present, but
corporate acceptance is pending. Reports remain drafts; all live metrics remain subject
to Gate A reconciliation. There is no approved corporate caller authentication, endpoint,
database, durable history collector or scheduler. Process-local frozen drafts expire
after one hour / 25 reports; audit/cache data is volatile. Historical membership, deleted
results and overwritten attempts cannot be reconstructed. Forecast/readiness outputs
are conditional evidence, not sign-off. Observed blocker age is a bounded-evidence streak
or unknown, not an asserted full chronology. Non-Windows PDF font provisioning remains
untested. See `docs/v2-handoff.md` for per-gate VDI instructions and enterprise boundaries.

No merge to the default branch, extension publication, deployment or infrastructure
provisioning was performed. Changes are confined to the dedicated feature branch.
