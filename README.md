# Sandpiper QA Command Center

Live Azure DevOps QA reporting using React/TypeScript/Vite and ASP.NET Core Web API.
The original Visual Studio solution structure is restored. This is an incremental
reconciliation of the original backend and the previously pushed client, not a new app.

V2 provides eight modules in the shared Command Center: Overview, Test Explorer,
Live TCM, Productivity Tracker, DSR Center, TCR Center, Execution Intelligence and
Quality Intelligence. Data Reconciliation, Settings and Audit History support them.
See [V2 handoff](docs/v2-handoff.md) for functionality, VDI validation and deployment
constraints, [gate log](docs/v2-gates.md) and [changed files](docs/v2-changed-files.md).
Current live totals and all generated reports remain provisional until corporate
reconciliation; no demo fallback is used.

## Structure

```text
Sandpiper.QA.Dashboard.slnx
Sandpiper.QA.Dashboard.Server/
  Controllers/                   Existing diagnostic/template controllers
  Models/                        Original suite models and reporting contracts
  Services/
    Analytics/KpiCalculator.cs   Shared KPI and re-execution definitions
    Analytics/ReportingClock.cs  Europe/London cutoff periods
    AzureDevOpsClient.cs         Authenticated ADO pagination and throttling
    FrontierAnalyticsServices.cs Existing Frontier integration, reconciled
sandpiper.qa.dashboard.client/
  src/                          Existing React UI, now consuming the backend
  tests/                        Backend-client contract tests
  sandpiper.qa.dashboard.client.esproj
  vss-extension.json
tests/Sandpiper.QA.Dashboard.Server.Tests/
docs/reporting-rules.md
docs/implementation-report.md
AGENTS.md
```

## Run locally

Prerequisites: .NET 10 SDK (the original target framework), a compatible Visual
Studio installation for VisualStudioCredential, Node.js 22.12+ or 24 LTS, and your
organizational ADO access. No PAT is required by this implementation.

1. From the repository root run `npm --prefix sandpiper.qa.dashboard.client ci`.
2. Open `Sandpiper.QA.Dashboard.slnx` in Visual Studio, sign in with your authorized
   organizational account, and select the server's `http` launch profile.
3. The original SPA proxy launch configuration starts Vite at
   `http://localhost:64423`; its `/api` proxy targets the local API at port 5243.

For explicit two-terminal startup:

```powershell
# Terminal 1, repository root
dotnet run --project Sandpiper.QA.Dashboard.Server --launch-profile http -p:BuildClient=false

# Terminal 2, repository root (if Visual Studio/SPA proxy has not started Vite)
npm run dev
```

Alternatively use `--no-launch-profile` and set `ASPNETCORE_ENVIRONMENT=Development`
and `--urls http://localhost:5243` to run the backend without automatic SPA startup.
Do not start a second Vite instance on the same port.

Development organization/project/tenant defaults preserve the original configuration
in `appsettings.Development.json`. Override with environment variables such as
`AzureDevOps__Organization`, `AzureDevOps__Project`, `AzureDevOps__TenantId`, or an
ignored server `appsettings.Local.json`. Do not store secrets in committed settings.
`AzureDevOps__DsrCutoff` uses `HH:mm`, default `18:00`, Europe/London.

## Local access boundary

Data routes are Development-only and require a loopback remote address, a localhost
Host, an allowed Origin when supplied, and `X-Sandpiper-Client: dashboard`. The client
adds that header automatically. CORS is supplementary, not authentication. This is
a single-user developer API using that developer's Visual Studio identity; it is not
an enterprise multi-user authentication implementation.

`GET /api/health` verifies the API process only; `adoConnected` is null. The legacy
`/api/auth/check` can check token availability, not ADO resource authorization.
Existing Frontier analytics/suites/points route names remain available, and the
analytics response retains legacy top-level aggregate fields. All positive plan IDs
are supported, including Frontier AI 819259 and Frontier AI – R2 839718; plan discovery
comes from ADO, not a hardcoded allowlist.

## Reporting

See `docs/reporting-rules.md` for the authoritative rules. Primary pass rate includes
blocked and N/A in Executed. Secondary is Passed/(Passed+Failed+Blocked). Progress
excludes N/A from both its numerator and applicable denominator. Every raw status is
available in the API and UI. No frontend KPI formulas or demo fallback remain.

Suite filtering includes descendants. Overall totals deduplicate point identities;
suite rows are explicitly own-suite totals and must not be summed as rollups.

History requires an explicit offset-aware `[from, to)` period and repeat-baseline
start. The UI starts with Today between UK cutoff boundaries; before today's cutoff
this can be an in-progress period. Yesterday/Last 7 Days use the same configured
cutoff. `/api/reporting/dsr-period` returns the last completed DSR period. For TCR,
enter agreed release boundaries. Release-to-Date uses the entered baseline and the
selection time; it never invents a release date. Retained accessible result records
cannot recover deleted results, overwritten retries or historical scope.

## Build and test

```powershell
npm run build
npm run lint
npm test
npm --prefix sandpiper.qa.dashboard.client run test:e2e
dotnet build Sandpiper.QA.Dashboard.slnx -p:BuildClient=false -p:ShouldRunNpmInstall=false
dotnet test tests/Sandpiper.QA.Dashboard.Server.Tests -p:BuildClient=false
npm run package
```

The .esproj retains the original separate frontend build behavior; `npm run build`
performs TypeScript/Vite compilation. `BuildClient=false` allows backend/test builds
without recursively building the JavaScript project. Build outputs and VSIX files
are ignored by Git. Original weather template files are retained, unrelated to QA KPIs.

## Installed extension and enterprise hosting

The extension SDK lifecycle, contribution IDs and relative assets are retained.
`npm run package` creates the VSIX. Packaging is not a connectivity test.

An installed extension requires an approved HTTPS API endpoint and caller
authentication. `VITE_API_BASE_URL` supplies an environment-specific endpoint including
`/api`; production localhost URLs are rejected. With no endpoint, the widget clearly
reports missing configuration. The current backend deliberately returns 503 for
production data routes until enterprise authentication is implemented and approved.
No extension access token is blindly accepted by this API.

AXA hosting, managed database and workload identities remain pending approval. No
infrastructure was provisioned. Current local connectivity was tested, but live ADO
plan discovery returned 503 because VisualStudioCredential was unavailable on the
implementation machine. Installed-extension backend connectivity is unverified.

DSR/TCR drafts support frozen evidence, Excel, PDF and an email-ready summary. The
local report store is bounded process memory, not approved historical SQL storage.
No report is distributed automatically. Defect verification/readiness/forecast outputs
show evidence and limitations. See `VALIDATION.md` for actual checks; packaging and
mocked browser tests do not establish corporate integration.
