# Contributing

If this is your first time in the repo, run bootstrap:

PowerShell:

```powershell
pwsh -File scripts/bootstrap.ps1
```

bash/zsh:

```bash
sh scripts/bootstrap.sh
```

## Build and quality scope

| Command | Scope | Use when |
| --- | --- | --- |
| `dotnet build Turbo.Main/Turbo.Main.csproj` | Core emulator only | Default contributor build |
| `dotnet build Turbo.Cloud.sln` | Full integrated solution build (includes sample plugin project if present in solution) | One-window core+plugin workflow |
| `dotnet build ../turbo-sample-plugin/TurboSamplePlugin/TurboSamplePlugin.csproj` | Sample plugin only | Plugin iteration |

The repo's default quality/build contract is project-scoped to `Turbo.Main/Turbo.Main.csproj`.

### Fast check (pre-commit)

```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
```

This is the default local commit gate and should stay fast.

### Full gate (pre-push + CI)

```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
```

This includes formatting/style/analyzer enforcement plus build and is required before pushing.

Policy phase controls:
- Current default: `TurboAIPolicyPhase=1` (warn-first rollout).
- Preview strict phase locally:

```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate -p:TurboAIPolicyPhase=2
```

## Integrated plugin workflow

Canonical integrated workflow is documented in `../turbo-sample-plugin/README.md`.
Run from `turbo-cloud` root:
- PowerShell: `pwsh -File ../turbo-sample-plugin/scripts/dev-integrated.ps1`
- bash/zsh: `sh ../turbo-sample-plugin/scripts/dev-integrated.sh`

## Toolchain pinning

Toolchain versions are pinned for consistent local and CI behavior:
- .NET SDK pin: `global.json`
- Local tools pin: `.config/dotnet-tools.json`

When updating these, include the version bump and a passing `TurboCloudQualityGate` run in the same change.

## Git hooks

Repository-managed hooks are in `.githooks`:
- `pre-commit` -> `TurboCloudFastCheck`
- `pre-push` -> `TurboCloudQualityGate`

Enable them if needed:

```bash
git config --local core.hooksPath .githooks
```

## AI-assisted contribution policy

Canonical instructions for all AI tools:
- `AGENTS.md` (coding constraints, review expectations, and validation contract)
- `CONTEXT.md` (architecture and dependency boundaries)
- `docs/patterns/` (golden sample implementations)

Copilot-specific adapter:
- `.github/copilot-instructions.md`

### Prompt recipe (Claude/Gemini/Copilot/other)
1. Include exact file paths being changed.
2. Attach `AGENTS.md` and `CONTEXT.md`.
3. Reference one matching file in `docs/patterns/`.
4. Require failure-mode handling and explicit verification commands.

### Boost-style prompt contract
Use this structure for all AI requests:
1. Goal
2. Target files
3. Required context files
4. Invariants to preserve
5. Forbidden changes
6. Validation commands
7. Expected output format (diff summary + risk notes + command results)

Task-specific recipes are maintained in `AGENTS.md`:
- Add packet handler
- Change grain behavior
- Add message/composer mapping
- Refactor lookup/cache logic

### PR review requirements for AI-generated code
1. Author must disclose AI usage in the PR template.
2. Author must explain complex generated logic in their own words.
3. Reviewer must verify at least one edge-case or failure-path test/update.

## AI contract maintenance
- If architecture flow changes, update `CONTEXT.md` in the same PR.
- If a recurring task pattern emerges, add/update a task recipe in `AGENTS.md`.
- Keep plugin mirror rules concise and aligned with core source-of-truth docs.
- Remove conflicting or duplicated guidance when contract files diverge.

## Local development config

`appsettings.Development.json` is local-only and must not be committed.

Recommended flow:
1. Keep shared defaults in `appsettings.json`.
2. Copy to `appsettings.Development.json` if missing.
3. Put machine-specific settings in `appsettings.Development.json`.

## Run in Development mode

PowerShell:

```powershell
$env:DOTNET_ENVIRONMENT="Development"; dotnet run --project Turbo.Main/Turbo.Main.csproj
```

bash/zsh:

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project Turbo.Main/Turbo.Main.csproj
```

## Troubleshooting MySQL connection

If you get `Unable to connect to any of the specified MySQL hosts`:
1. Verify `Turbo:Database:ConnectionString` in `appsettings.Development.json`.
2. Verify MySQL is listening on that host and port.
3. Verify no `TURBO__...` environment variable overrides the connection string.

## Troubleshooting integrated solution build

If `dotnet build Turbo.Cloud.sln` fails due to plugin state but core work is unaffected, fall back to:

```bash
dotnet build Turbo.Main/Turbo.Main.csproj
```

## Orleans design and lifecycle checks

For grain mental model, snapshots guidance, and session/presence cleanup test checklist, see `docs/orleans.md`.
