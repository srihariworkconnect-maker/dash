# Reconciliation implementation report

Date: 19 October 2026. Scope: original ASP.NET Core/React solution reconciliation,
shared backend KPI rules, automated regression tests, and local connectivity.
This report was prepared before the user's subsequent authorization to publish.
The reconciliation starts from c87eb23; see Git history for the published commit.

## What changed

Restored the original solution and server from the uploaded ZIP, omitting generated
bin/obj outputs and user-specific project settings. Moved the existing React client
back to `sandpiper.qa.dashboard.client/`, preserving its styles, suite explorer and
extension identity. Reused the FrontierAnalyticsService and original API paths.
Removed the duplicate browser KPI implementation: all calculations now come from
the ASP.NET Core KpiCalculator. No demo fallback was introduced.

The primary denominator includes blocked and N/A. Secondary is P/(P+F+B). Progress
uses executed applicable points over applicable scope, excluding N/A from both.
Status categories remain separately inspectable. History uses distinct completed
run/result records inside an explicit reporting window and selected current scope;
blocked records can qualify, but duplicate identities do not create new attempts.

## Every changed/restored source file

### Root and documentation

- `.gitignore`: ignore backend build outputs, user settings and local configuration.
- `AGENTS.md`: requirements, latest reporting decisions and reconciled repository status.
- `Sandpiper.QA.Dashboard.slnx`: original solution restored; backend tests added.
- `package.json`: root convenience commands targeting the restored client folder.
- `README.md`: reconciled architecture, Visual Studio/CLI setup and honest deployment status.
- `VALIDATION.md`: actual checks, unsuccessful attempts and limitations.
- `docs/reporting-rules.md`: agreed formulas, scope rules and non-core outcome policy.
- `docs/implementation-report.md`: this report.

### Server files restored and modified from the ZIP

- `Sandpiper.QA.Dashboard.Server/Program.cs`: dependency registration, original live
  route names, local request boundary, sanitized errors, reporting-period/history routes.
- `Sandpiper.QA.Dashboard.Server/Sandpiper.QA.Dashboard.Server.csproj`: original framework
  retained; SPA proxy version pinned, client URL reconciled and backend-only build option.
- `Sandpiper.QA.Dashboard.Server/Controllers/TestAnalyticsController.cs`: diagnostic
  service registration now works; health does not claim ADO authorization.
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsServices.cs`: original URL helper
  uses environment-configured organization/project; original diagnostic routes retained.
- `Sandpiper.QA.Dashboard.Server/Services/FrontierAnalyticsServices.cs`: original service
  reconciled with reusable ADO transport, dynamic plans, subtree scope, deduplication,
  shared KPIs and scoped history. Legacy top-level aggregate fields retained.
- `Sandpiper.QA.Dashboard.Server/appsettings.Development.json`: original non-secret
  org/project/tenant values moved to development settings, bounded concurrency,
  local allowed origins and configurable cutoff. No credentials added.

### New server files

- `Sandpiper.QA.Dashboard.Server/Models/Analytics.cs`: shared metric, suite, period,
  point, result-attempt and analytics response contracts.
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsClient.cs`: complete continuation/
  offset pagination, cancellation, global request bound and Retry-After handling.
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsOptions.cs`: environment configuration.
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/KpiCalculator.cs`: authoritative
  latest-point and distinct recorded-attempt calculations.
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ReportingClock.cs`: UK cutoff periods.

### Original server files restored unchanged

- `Sandpiper.QA.Dashboard.Server/CHANGELOG.md`
- `Sandpiper.QA.Dashboard.Server/Controllers/WeatherForecastController.cs`
- `Sandpiper.QA.Dashboard.Server/Models/AdoTestSuite.cs`
- `Sandpiper.QA.Dashboard.Server/Properties/launchSettings.json`
- `Sandpiper.QA.Dashboard.Server/Sandpiper.QA.Dashboard.Server.http`
- `Sandpiper.QA.Dashboard.Server/WeatherForecast.cs`
- `Sandpiper.QA.Dashboard.Server/appsettings.json`

Weather files are original unrelated scaffold, not production QA data.

### Client files moved and modified from the previously pushed implementation

- `src/App.tsx` → `sandpiper.qa.dashboard.client/src/App.tsx`: preserve dashboard UI;
  display backend rates, all statuses, UK times, subtree scope and period-based history.
- `src/main.tsx` → `sandpiper.qa.dashboard.client/src/main.tsx`: standalone and extension
  startup, environment-specific API configuration and clear unavailable-state handling.
- `src/adoExtension.ts` → `sandpiper.qa.dashboard.client/src/adoExtension.ts`: retain
  SDK widget lifecycle; await initialization handshake safely.
- `src/index.css` → `sandpiper.qa.dashboard.client/src/index.css`: history input styling.
- `src/services/adoApi.ts` → `sandpiper.qa.dashboard.client/src/services/adoApi.ts`:
  backend contract client replacing direct browser aggregation; cancellation,
  response-scope checks, configured URL validation and local protection header.
- `vite.config.ts` → `sandpiper.qa.dashboard.client/vite.config.ts`: local /api proxy
  and environment override; relative production assets retained.
- `vss-extension.json` → `sandpiper.qa.dashboard.client/vss-extension.json`: original
  extension identity preserved, descriptions clarify backend configuration requirement.

### Client files moved without content changes

- `eslint.config.js` → `sandpiper.qa.dashboard.client/eslint.config.js`
- `index.html` → `sandpiper.qa.dashboard.client/index.html`
- `package.json` → `sandpiper.qa.dashboard.client/package.json`
- `package-lock.json` → `sandpiper.qa.dashboard.client/package-lock.json`
- `public/favicon.png` → `sandpiper.qa.dashboard.client/public/favicon.png`
- `src/App.css` → `sandpiper.qa.dashboard.client/src/App.css`
- `src/components/TestSuiteTree.tsx` → `sandpiper.qa.dashboard.client/src/components/TestSuiteTree.tsx`
- `src/types/ado.ts` → `sandpiper.qa.dashboard.client/src/types/ado.ts`
- `tsconfig.app.json` → `sandpiper.qa.dashboard.client/tsconfig.app.json`
- `tsconfig.json` → `sandpiper.qa.dashboard.client/tsconfig.json`
- `tsconfig.node.json` → `sandpiper.qa.dashboard.client/tsconfig.node.json`

### Other client additions/restorations

- `sandpiper.qa.dashboard.client/.env.example`: non-secret endpoint/proxy examples.
- `sandpiper.qa.dashboard.client/CHANGELOG.md`: original Visual Studio scaffold notes restored.
- `sandpiper.qa.dashboard.client/sandpiper.qa.dashboard.client.esproj`: original project
  restored; obsolete Vitest declaration removed in favor of actual Node test commands.
- `sandpiper.qa.dashboard.client/tests/backend-client.test.cjs`: six frontend contract tests.

### Removed duplicate implementations

- Former `src/services/qaAnalytics.ts` (temporarily moved under the client directory)
  is removed because backend KpiCalculator now owns the reporting rules.
- Former `tests/analytics.test.cjs` is superseded by backend KPI/ADO tests and the new
  client contract tests. Its previous assertions excluded blocked from execution and
  no longer represented the user's agreed reporting rules.

### New backend test files

- `tests/Sandpiper.QA.Dashboard.Server.Tests/Sandpiper.QA.Dashboard.Server.Tests.csproj`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/AnalyticsTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/AdoIntegrationTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ApiContractTests.cs`

Unused demo data, old demo assets, generated VSIX/dist outputs, local secrets and
user-specific .csproj.user files were not restored into versioned source.

## Results

| Check | Result |
| --- | --- |
| Restored .slnx build | Passed; zero warnings/errors, client compiled separately |
| TypeScript/Vite build | Passed |
| ESLint | Passed |
| Backend tests | 24 passed |
| Frontend tests | 6 passed |
| VSIX packaging | Passed; not installed or published |
| Real local API health | Passed |
| Real Vite → ASP.NET proxy health and reporting period | Passed |
| Invalid local scope / missing protection header | 400 / 403 as expected |
| Live ADO plan discovery | 503: local VisualStudioCredential unavailable |
| Installed-extension backend connection | Not tested; no approved endpoint/authentication |

The automated fixtures exercise plan IDs 819259, 839718 and an additional plan;
they do not prove these plans' actual live figures. No confidential ADO payloads
were written to fixtures or reports. Temporary servers were stopped after testing.

## Remaining limitations

See VALIDATION.md for full details. Principal next action is VDI organizational
sign-in and evidence-based live comparison for both Frontier plans. Enterprise
identity/hosting/database approval, persistent history, DSR/TCR exports and broader
analytics remain outstanding. Non-core outcome classification is explicit but needs
comparison with organizational reporting conventions. This increment does not
establish installed-extension connectivity or recover deleted/overwritten history.
