# Continuous integration

`Code Quality` runs for pull requests, pushes to `main`, and manual dispatches.
New PR commits cancel older runs for that PR. Each platform keeps its existing
`quality-<OS>` status name.

Linux runs workflow linting, CSharpier, governance, style and analyzer verification.
Linux, Windows and macOS each compile Turbo with its build analyzers and Orleans
generators. NuGet package caches are separated by OS and architecture; restore
still runs on cache hits.

PRs containing only added or modified Markdown under `docs/` or the document
allowlist in `scripts/ci_scope.py` take the lightweight path. Required governance
files and changed document contents are checked, and all platform jobs report
success without installing .NET. Deletions, renames, code/configuration changes,
and uncertain diffs keep the full checks. Main and manual runs always use them.

Failed restore/build steps upload MSBuild binary logs for three days. Project
imports are not embedded, but logs can still contain build properties and runner
paths. Successful builds do not upload these diagnostics.

Dependabot checks GitHub Actions and NuGet weekly, grouping minor/patch updates
per ecosystem. Major upgrades remain separate, with EF major upgrades held until
the MySQL provider is compatible. Dependency PRs still require review.

Local workflow validation: `actionlint .github/workflows/quality.yml`.
The existing `TurboCloudFastCheck` and `TurboCloudQualityGate` MSBuild targets
remain available for local code validation.
