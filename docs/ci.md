# Continuous integration

- [Introduction](#introduction)

## Introduction

`Code Quality` runs for pull requests, merge queue groups (`merge_group`), pushes to
`main`, a nightly schedule and manual dispatches. New PR commits cancel older runs for
that PR; every `main` push keeps its own run.

| Job | PRs and merge groups | `main`, nightly, manual |
| --- | --- | --- |
| `build-ubuntu-latest`: CI script tests, workflow lint, build of `Turbo.Tests` (every project) with analyzers and code style | yes | yes |
| `test-ubuntu-latest (n/4)`: the test suite in four shards | yes | yes |
| `lint`: CSharpier, governance, style and analyzer fixes | yes | yes |
| `quality-windows-latest`, `quality-macos-latest`: build without analyzers and the whole suite | no | yes (manual: `platforms` input) |

A PR waits for the slowest Linux shard instead of the slowest platform. Windows and
macOS run after the merge and every night; to run them on a PR branch before merging,
dispatch the workflow on that branch (`gh workflow run quality.yml --ref <branch>`).
In the 1000 runs before the split (2026-08-28 to 2026-10-24) they failed alone 29
times, each time on a timing-sensitive test that also failed on Linux or on a runner
timeout, never on a platform difference.

`scripts/ci_test_shards.py` lists the namespaces of each shard; a test belongs to the
shard with the longest prefix it starts with, and anything unlisted runs in the last
shard, so a new namespace is never skipped. Each shard uploads a TRX file
(`test-results-<n>`, seven days) with per-test durations; move a namespace between
shards when one shard's `Test` step runs well over the others.

NuGet package caches are separated by OS and architecture; restore still runs on
cache hits.

PRs containing only added or modified Markdown under `docs/` or the document
allowlist in `scripts/ci_scope.py` take the lightweight path. Required governance
files and changed document contents are checked, and every PR job reports success
without installing .NET. Deletions, renames, code/configuration changes, and
uncertain diffs keep the full checks. Main, merge group and manual runs always use
them.

Failed restore/build steps upload MSBuild binary logs for three days. Project
imports are not embedded, but logs can still contain build properties and runner
paths. Successful builds do not upload these diagnostics.

Dependabot checks GitHub Actions and NuGet weekly, grouping minor/patch updates
per ecosystem. Major upgrades remain separate, with EF major upgrades held until
the MySQL provider is compatible. Dependency PRs still require review.

Local workflow validation: `actionlint .github/workflows/quality.yml`.
The existing `TurboCloudFastCheck` and `TurboCloudQualityGate` MSBuild targets
remain available for local code validation.


