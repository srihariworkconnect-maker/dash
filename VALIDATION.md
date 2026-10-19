# Validation — local reconciliation, 19 October 2026

## Passed

- Complete restored .slnx solution build, using `BuildClient=false` and
  `ShouldRunNpmInstall=false`: zero warnings and zero errors. Frontend compiled separately.
- React TypeScript/Vite production build.
- ESLint.
- 24 backend tests: shared pass rates, blocked/N/A/progress denominators, all-status
  visibility, duplicate/conflicting identities, scoped distinct blocked attempts,
  missing history evidence, UK DST cutoffs, ADO pagination and both Frontier plans
  plus another dynamic plan, existing routes, local request protection, production
  fail-closed behavior and invalid reporting periods.
- Six client tests: environment URL constraints, request protection header and
  cancellation, response scope checks, HTML rejection, period/subtree forwarding,
  and hierarchy construction.
- VSIX packaging. This is not installed-widget authentication or connectivity testing.
- Actual local HTTP probe through Vite at port 64423 to ASP.NET at port 5243:
  direct and proxied health returned Healthy; proxied DSR period returned a valid
  Europe/London cutoff period; invalid plan ID returned 400; missing protection
  header returned 403. No fabricated analytics data was used in that probe.

## Environment and unsuccessful attempts

- The machine initially had no .NET SDK. A workspace-local .NET 10.0.402 SDK was
  installed outside the repository; it is not part of source control.
- Sandbox socket restrictions initially prevented testhost/HTTP connections. The
  checks passed when rerun with local networking permissions.
- A build attempted while the smoke-test server was running encountered a locked
  executable. The server was stopped and the full build passed afterward.
- A read-only live ADO plan-discovery probe through the local backend returned 503;
  server classification was CredentialUnavailableException. No live ADO analytics
  or historical data was validated, and no confidential payload was persisted.
- Visual Studio interactive F5 launch was not exercised; CLI startup, .slnx build
  and the actual Vite-to-ASP.NET proxy were exercised.
- Temporary development servers were stopped after the checks.

## Remaining limitations

- Authenticate in the user's VDI and compare both Frontier plans against actual ADO
  evidence before calling live behavior validated. Tests use controlled fixtures.
- Installed-extension backend endpoint/authentication and AXA hosting are unapproved
  and untested. Production data routes fail closed intentionally.
- Current identity mode is loopback-only VisualStudioCredential for local single-user
  development, not production authorization. Token acquisition is reused by a singleton
  credential; no shared data cache is introduced before caller scope is defined.
- Pagination is bounded and retries handle 429/503. History scans can still be costly.
  No database, background collector, persistent snapshots or transactionally frozen
  ADO snapshot exists. Scope may change during collection; conflicting evidence fails.
- Re-execution is based on distinct completed run/result records and current point
  scope in the selected window. Deleted history, rewritten attempts, iterations and
  historical membership are not reconstructed. Missing dates/links withhold the total.
- Non-core outcomes retain an explicit documented policy, pending comparison with
  organizational ADO reporting conventions. No claim of exact built-in chart parity.
- The lockfile retains two moderate development-tool findings (tfx-cli/uuid chain),
  previously identified by npm audit. Those dependencies are not in the browser bundle.
- Complete DSR/TCR documents, productivity, defects/risk, leadership views and
  scheduled delivery are not implemented in this increment.

Validation was completed before the user authorized committing and pushing this implementation.
