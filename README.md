# Sandpiper QA Command Center — live ADO widget

This is a complete replacement CLIENT project. It uses only live Azure DevOps data.
There are no demo data files, sample KPI figures, PATs, localhost API calls, or ASP.NET dependencies.
The original uploaded source and backend have not been modified.

## Install in your VDI

1. Back up your existing `sandpiper.qa.dashboard.client` folder.
2. Clone this repository into a NEW folder and open the repository root in VS Code.
   All client files are at the repository root. Do not merge its `src` folder with
   the old project: that would retain old demo files.
3. Use Node.js 22.12+ or Node.js 24 LTS, with access to your approved npm registry.
4. From this folder run:

   ```powershell
   npm install
   npm run build
   npm run lint
   npm test
   npm run package
   ```

5. The manifest retains publisher `SandpiperQualityEngineering`, extension ID
   `sandpiper-quality-insights-dashboard`, and widget ID `sandpiper-qa-widget` from
   the supplied ZIP. Confirm these identify the extension you actually installed.
   Version is `1.0.1`; raise it above the installed version if necessary.
6. Upload the generated VSIX as an update to that private extension in the Visual
   Studio Marketplace publishing portal. Share/install it in your organization.
7. Open the intended PROJECT > Overview > Dashboards and add/reload the widget.
   The signed-in user needs Test Plans access. The extension requests `vso.test` read scope.

Opening index.html or localhost directly intentionally shows an instruction to open
Azure DevOps. No fabricated figures are substituted when authentication is absent.

This client is independent of the original Visual Studio .slnx and .esproj.
Run the commands above from this new folder; do not run the old ASP.NET solution
to launch this replacement. Keep your backend source separately if needed for other work.

## Full source map

| File | Purpose |
| --- | --- |
| src/main.tsx | Startup and initialization error screen |
| src/adoExtension.ts | ADO SDK handshake and widget load/reload registration |
| src/App.tsx | Live plan selection, snapshots, errors, refresh, history and suite explorer |
| src/services/adoApi.ts | Authenticated GET requests, pagination, throttling handling and cancellation |
| src/services/qaAnalytics.ts | Latest-outcome KPIs and live re-execution aggregation |
| src/types/ado.ts | ADO response types |
| src/components/TestSuiteTree.tsx | Expandable suite hierarchy |
| src/App.css, src/index.css | Dashboard styling |
| vite.config.ts | Relative production asset paths |
| vss-extension.json | Private ADO widget package definition |
| package.json, tsconfig*.json, eslint.config.js | Dependencies and build configuration |

## KPI policy

- Total = distinct current test-point IDs in all returned suites.
- Executed = current outcome passed, failed, inconclusive, timeout, aborted or error.
- Applicable = total minus notApplicable.
- Execution progress = executed / applicable, or 0 when denominator is 0.
- Pass rate = passed / executed, or 0 when denominator is 0.
- Blocked, pending, paused/in-progress and unknown are displayed separately.
- Missing, unspecified or unfamiliar outcomes are unknown, never automatically executed.
- These are explicit dashboard definitions, not a promise to match every ADO built-in chart.
- Snapshots are fetched on load, plan change and refresh. There is no automatic polling.
  ADO can change while pages are being fetched; this is not a transactional snapshot.

## Re-execution

Click **Load live execution history** after the current plan has loaded. This is
separate because reading all retained runs and results can be expensive.

- Scope is all accessible retained runs associated with the selected plan, restricted
  to point IDs currently in the plan. There is NO implicit release/date window.
- Each distinct `(run ID, result ID)` counts once if its state is Completed and its
  outcome is one of the completed execution outcomes above.
- Re-executed points = distinct point IDs with at least two qualifying result records.
- Additional executions = sum of `(count - 1)` across those points.
- Iterations, test steps, blocked outcomes and N/A outcomes do not count separately.
- Missing test-point links are reported; a complete plan total is withheld in that case.
- Deleted/retention-expired executions cannot be recovered. This is retained result
  history, not an audit of every historical retry or every change to a result.

## Verification in your VDI

Build/lint and installed ADO operation must be verified in your environment.
Test a paginated plan, all outcome categories, changing plans during loading,
permission errors, refresh, and two distinct completed results for the same point.
Check the browser network panel: no request should target localhost.

Validation completed locally: TypeScript/Vite production build, ESLint, seven
automated KPI/history/pagination checks, and VSIX packaging all passed.
An authenticated installed-widget test in your VDI is still required.

Dependency versions follow the supplied project's manifests except the packaging
tool, pinned to tfx-cli 0.22.1 after audit identified high-severity dependencies in
the original version. Two moderate audit findings remain in the packaging tool's
uuid dependency chain. This is development tooling and is not bundled into the
browser widget. See VALIDATION.md for the scope of verification.

## API references

- https://learn.microsoft.com/en-us/rest/api/azure/devops/testplan/test-plans/list?view=azure-devops-rest-7.1
- https://learn.microsoft.com/en-us/rest/api/azure/devops/testplan/test-suites/get-test-suites-for-plan?view=azure-devops-rest-7.1
- https://learn.microsoft.com/en-us/rest/api/azure/devops/testplan/test-point/get-points-list?view=azure-devops-rest-7.1
- https://learn.microsoft.com/en-us/rest/api/azure/devops/test/runs/list?view=azure-devops-rest-7.1
- https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/list?view=azure-devops-rest-7.1
