# Turbo Cloud

## 5-Minute Quickstart
1. Clone and enter the repo.
2. Run bootstrap once.
3. Set your local DB connection string.
4. Run the app in Development mode.

Clone:

```bash
git clone <your-repo-url> turbo-cloud
cd turbo-cloud
```

Bootstrap (PowerShell):

```powershell
pwsh -File scripts/bootstrap.ps1
```

Bootstrap (bash/zsh):

```bash
sh scripts/bootstrap.sh
```

Set `Turbo:Database:ConnectionString` in `appsettings.Development.json`, then run:

PowerShell:

```powershell
$env:DOTNET_ENVIRONMENT="Development"; dotnet run --project Turbo.Main/Turbo.Main.csproj
```

bash/zsh:

```bash
DOTNET_ENVIRONMENT=Development dotnet run --project Turbo.Main/Turbo.Main.csproj
```

## What This Repository Is
`Turbo.Cloud.sln` is the main Turbo emulator solution.
It includes the host executable (`Turbo.Main`), domain modules (`Turbo.Rooms`, `Turbo.Players`, `Turbo.Database`, and others), networking/message layers, and plugin infrastructure.

## Tooling Baseline
- .NET SDK 9.x (pinned via `global.json`)
- Git
- MySQL running locally (or reachable dev instance)

Check SDK:

```bash
dotnet --version
```

## Local Configuration
- `appsettings.json` contains shared defaults.
- `appsettings.Development.json` is local-only and gitignored.
- The bootstrap script creates `appsettings.Development.json` from `appsettings.json` if missing.

## Quality Model (Two-Phase)
- Fast local commit check:
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
- Full quality gate (pre-push + CI):
  - `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`
- AI policy rollout phase:
  - Default is `TurboAIPolicyPhase=1` (warn-first).
  - Preview strict mode with `-p:TurboAIPolicyPhase=2`.

Hooks are repository-managed in `.githooks`:
- `pre-commit` runs the fast check.
- `pre-push` runs the full quality gate.

## Build Scope Matrix
| Command | Scope | Default? | Use when |
| --- | --- | --- | --- |
| `dotnet build Turbo.Main/Turbo.Main.csproj` | Core emulator only | Yes | Normal core development and CI-compatible local checks |
| `dotnet build Turbo.Cloud.sln` | All projects currently in solution (including sample plugin) | No | One-window integrated core + plugin work |
| `dotnet build ../turbo-sample-plugin/TurboSamplePlugin/TurboSamplePlugin.csproj` | Sample plugin only | No | Plugin-only iteration |

`TurboSamplePlugin` intentionally stays in `Turbo.Cloud.sln` for IDE convenience, but the default repo build contract is project-scoped to `Turbo.Main`.

## Daily Commands
- Core build (default): `dotnet build Turbo.Main/Turbo.Main.csproj`
- Integrated solution build (optional): `dotnet build Turbo.Cloud.sln`
- Plugin build only (optional): `dotnet build ../turbo-sample-plugin/TurboSamplePlugin/TurboSamplePlugin.csproj`
- Fast checks: `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
- Full quality gate: `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`
- Run in Development: `dotnet run --project Turbo.Main/Turbo.Main.csproj`

## Local Dev Plugins
Plugin loading supports both the runtime plugin folder and dev-specific paths:
- Default folder: `<runtime>/plugins`
- Optional config: `Turbo:Plugin:DevPluginPaths`

Example:

```json
{
  "Turbo": {
    "Plugin": {
      "DevPluginPaths": [
        "C:/Users/you/RiderProjects/turbo-sample-plugin/TurboSamplePlugin/bin/Debug/net9.0"
      ]
    }
  }
}
```

If the same plugin key exists in both places, `DevPluginPaths` wins and a warning is logged.

### Integrated plugin dev loop (single terminal flow)
Canonical integrated workflow lives in the plugin repo:
- Guide: `../turbo-sample-plugin/README.md`
- PowerShell: `pwsh -File ../turbo-sample-plugin/scripts/dev-integrated.ps1`
- bash/zsh: `sh ../turbo-sample-plugin/scripts/dev-integrated.sh`

## Orleans Notes
Turbo Cloud uses Orleans as its core runtime model for stateful domain workflows.
For project-specific Orleans guidance, see `docs/orleans.md`.

## Troubleshooting
### MySQL connection errors
If you see `Unable to connect to any of the specified MySQL hosts`:
1. Verify `Turbo:Database:ConnectionString` in `appsettings.Development.json`.
2. Verify MySQL host/port are reachable.
3. Verify no `TURBO__...` environment variables override your local setting.

### Development file not loading
1. Ensure `DOTNET_ENVIRONMENT=Development` when running.
2. Confirm `appsettings.Development.json` exists at repo root.

### Quality check failures
1. Run `dotnet tool restore`.
2. Run `dotnet csharpier .`.
3. Run `dotnet format style`.
4. Run `dotnet format analyzers`.
5. Re-run `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`.

### Solution build fails but core build passes
If `dotnet build Turbo.Cloud.sln` fails because of plugin project state, use the default core build command:

```bash
dotnet build Turbo.Main/Turbo.Main.csproj
```

## AI-Assisted Development
Canonical AI context files:
- `AGENTS.md` (coding contract and review rules)
- `CONTEXT.md` (architecture boundaries and placement rules)
- `docs/patterns/` (golden implementation examples)

Tool-specific adapters:
- `.github/copilot-instructions.md` (GitHub Copilot)
- `CLAUDE.md` (Claude)
- `CODEX.md` (Codex)

Prompt recipe for any AI tool:
1. Include task + exact target file paths.
2. Attach `AGENTS.md` and `CONTEXT.md`.
3. Reference one relevant file from `docs/patterns/`.
4. Ask for edge-case handling and validation commands.

Boost-style prompting pack:
- portable prompt contract + task recipes live in `AGENTS.md`
- architecture invariants live in `CONTEXT.md`
- tool adapters live in `.github/copilot-instructions.md`, `CLAUDE.md`, and `CODEX.md`