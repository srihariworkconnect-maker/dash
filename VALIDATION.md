# Verification

Completed 19 October 2026:

- npm run build: passed, including TypeScript checks and Vite production build.
- npm run lint: passed.
- npm test: seven tests passed.
- npm run package: passed and generated version 1.0.1 VSIX.
- Production assets use relative paths in dist/index.html.
- No demo-data imports or localhost API URLs exist in application source.

Automated tests cover zero denominators, unexecuted and missing outcomes,
point deduplication and denominator policy, distinct history result aggregation,
continuation-token query encoding, repeated-token rejection, and offset pagination
when the server returns short pages.

The tests use controlled API responses; they do not access a real ADO organization.
They do not establish that ADO CORS headers, host initialization, user permissions,
or actual retained history work in your environment. Verify these by installing
the VSIX in a test dashboard before using the numbers for release decisions.

Security audit: after pinning tfx-cli to 0.22.1, no high or critical findings were
reported. Two moderate findings remain: uuid <11.1.1 and its parent tfx-cli.
These affect packaging dependencies and are absent from the production browser
bundle. Do not run npm audit fix --force without reviewing the resulting toolchain.

The supplied original source and ASP.NET backend remain unchanged. This deliverable
is an independent live-data replacement client.
