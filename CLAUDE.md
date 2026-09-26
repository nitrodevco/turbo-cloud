# Claude Adapter (Turbo Cloud)

This adapter points Claude to the canonical AI contract for this repository.

## Required context load order
1. `AGENTS.md`
2. `CONTEXT.md`
3. One relevant sample in `docs/patterns/`
4. `.github/copilot-instructions.md` (tool adapter parity rules)

## Non-negotiable constraints
- Keep packet handlers orchestration-only.
- Do not query database contexts/repositories from packet handlers.
- Do not send composers directly to sockets/sessions from handlers. There are exactly three ways
  to send one: `ctx.SendComposerAsync` to the session being handled,
  `grainFactory.SendComposerToPlayerAsync` / `SendComposerToPlayersAsync` to a player, and
  `RoomGrain.SendComposerToRoomAsync` to a room (or `RoomGrain.SendComposerToRoomAndForget`,
  its fire-and-forget form, from room code that must not wait). Do not spell out
  `GetPlayerPresenceGrain(id).SendComposerAsync(...)` or add a local send helper.
- For `Revision<id>` parser/serializer work, edit `Turbo.Revisions/Revision<id>/**`.

## Validation commands
```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
```
