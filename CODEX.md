# Codex Adapter (Turbo Cloud)

This adapter points Codex to the canonical AI contract for this repository.

## Required context load order
1. `AGENTS.md`
2. `CONTEXT.md`
3. One relevant sample in `docs/patterns/`
4. `.github/copilot-instructions.md` (tool adapter parity rules)

## Non-negotiable constraints
- Keep packet handlers orchestration-only.
- Do not query database contexts/repositories from packet handlers.
- Do not send composers directly to sockets/sessions from handlers; route via `PlayerPresenceGrain.SendComposerAsync`.
- For `Revision<id>` parser/serializer work, edit `../turbo-sample-plugin/TurboSamplePlugin/Revision/**`, not `turbo-cloud`.

## Validation commands
```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
```
