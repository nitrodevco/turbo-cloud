# Copilot Instructions (Turbo Cloud)

Before suggesting code, read:
1. `AGENTS.md` (canonical AI coding contract)
2. `CONTEXT.md` (architecture and placement boundaries)
3. One relevant sample in `docs/patterns/`

## Repository expectations
- Keep output compatible with repository quality gates (`csharpier`, `dotnet format`, analyzers, build).
- Follow `.editorconfig` and local naming/style conventions.
- Keep changes minimal and scoped to the task.
- Match patterns in nearby files before adding new abstractions.
- Include guard clauses and failure-path behavior where relevant.

## Prompt contract (short form)
Include in every request:
1. Goal
2. Target files
3. Required context files (`AGENTS.md`, `CONTEXT.md`, nearby pattern file)
4. Invariants to preserve
5. Forbidden changes
6. Validation commands

## Core-specific constraints
- Keep host composition in `Turbo.Main`.
- Keep plugin lifecycle logic centralized in `Turbo.Plugins`.
- Keep packet handlers in `Turbo.PacketHandlers` and domain logic in domain modules.
- Respect Orleans grain boundaries and avoid bypassing grain orchestration.
- For protocol revision parser/serializer work, target the plugin repo path:
  - `../turbo-sample-plugin/TurboSamplePlugin/Revision/**`
  - Do not generate `Revision<id>/Parsers` or `Revision<id>/Serializers` in `turbo-cloud`.
- For extended profile flow:
  - keep handlers orchestration-only
  - do not query database contexts/repositories from handlers
  - use canonical grain access patterns
  - avoid silent catch blocks
  - map available snapshot fields into composer payloads
  - keep incoming message contracts explicit for required fields
- For session/presence/room flow:
  - do not send composers directly to raw sessions/sockets from handlers
  - target players through `PlayerPresenceGrain.SendComposerAsync`
  - keep active-room indexing and keepalive behavior in `RoomDirectoryGrain`
  - treat `[KeepAlive]` as explicit infrastructure-only usage

## Orleans grain constraints
- Never use bare `catch { }` — always `catch (Exception ex)` with `ILogger<T>`.
- Parallelize independent grain calls with `Task.WhenAll`, not sequential `await` in loops.
- Hoist repeated grain calls out of loops.
- Batch DB deletes with `WHERE ... IN (...)`, not per-entity `ExecuteDeleteAsync`.
- Use timer-flush for housekeeping writes (see `RoomPersistenceGrain`).
- No hardcoded limits in grains — pass from handlers via `IConfiguration`.
- Use tracked EF deletes when atomicity with inserts is required.
- Replace `.Ignore()` with a `LogAndForget` helper that logs faulted tasks.
- Cap in-memory per-event collections (message history, queues).

## Task routing hints
- Handler task: use neighboring handler + `Turbo.Primitives/Orleans/GrainFactoryExtensions.cs`.
- Grain task: use grain interface + snapshot/state types as primary references.
- Message/composer task: update incoming/outgoing contracts and handler mapping together.
- Lookup/cache task: preserve bidirectional + case-insensitive behavior across set/invalidate/get flows.

## Validation guidance
Generated changes should pass:
- `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck`
- `dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate`
