# Turbo Cloud

The documentation starts at [docs/documentation.md](docs/documentation.md), from
[Installation](docs/installation.md) to writing plugins; `docs-site/` builds it into a website
([Contribution Guide](docs/contributions.md#building-the-site)).

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
- .NET SDK 10.x (pinned via `global.json`)
- Git
- MySQL or MariaDB running locally (or a reachable dev instance). Turbo creates and migrates its own database on first start; see [docs/database.md](docs/database.md)

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
A plugin is a folder of sources: `manifest.json`, its C# under `server/`, its client module under
`client/src/`, its files. The server compiles it as it loads it and loads no plugin assembly
(`docs/plugin-loading.md`). Plugins load from the runtime plugin folder (`<runtime>/plugins`) and
from `Turbo:Plugin:DevPluginPaths`:

```json
{
  "Turbo": {
    "Plugin": {
      "DevPluginPaths": [
        "C:/Users/you/plugins/skills/dist/skills"
      ]
    }
  }
}
```

If the same plugin key exists in both places, `DevPluginPaths` wins and a warning is logged. You can
list several plugin folders when developing several plugins at once.

A new plugin with a server half and a client module: `dotnet new turbo-plugin` from `templates/turbo-plugin` gives both, tests and one `npm run dev` (`docs/client-modules.md`, "Start from the template").
`npm run dev` keeps `dist/<key>/` current; in the Development environment with
`Turbo:Plugin:HotReloadEnabled`, a saved source compiles and reloads the plugin in-process.
`npm run check` compiles the sources as the server does.

A plugin made before the server compiled plugins (one that ships a `.dll`): `docs/upgrading.md`.
Every change that affects plugin authors, newest first: `docs/plugin-changelog.md`.

### Plugin hot-reload limitations
- Grain types cannot be hot-reloaded because Orleans grain type registration happens at silo startup.
- Memory may grow over many reloads if assembly references are retained; restart periodically during long sessions.

### Integrated plugin dev loop (single terminal flow)
Canonical integrated workflow lives in the plugin repo:
- Guide: `../turbo-sample-plugin/README.md`
- PowerShell: `pwsh -File ../turbo-sample-plugin/scripts/dev-integrated.ps1`
- bash/zsh: `sh ../turbo-sample-plugin/scripts/dev-integrated.sh`

## Orleans Notes
Turbo Cloud uses Orleans as its core runtime model for stateful domain workflows.
For project-specific Orleans guidance, see `docs/orleans.md`.

## Troubleshooting
### Database and migrations
Turbo migrates its database when it starts and explains anything it refuses to do; see
[docs/database.md](docs/database.md). `dotnet run --project Turbo.Main/Turbo.Main.csproj -- migrate --status`
says where the database stands without starting the server.

### MySQL connection errors
If you see `Unable to connect to any of the specified MySQL hosts`, or `Cannot use the database`:
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
