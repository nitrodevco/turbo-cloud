# Turbo Cloud AI Contract

This file is the coding contract for AI-assisted changes in `turbo-cloud`, for every tool. Codex
reads it itself, `CLAUDE.md` imports it, and `.github/copilot-instructions.md` points at it. Read
`CONTEXT.md` next: it says where code belongs and which type owns what.

This file holds the rules every change needs. The rules for one area are in `docs/rules/`, and
*Rules by area* below says when to read each page (Claude Code is also reminded by
`.claude/rules/` the moment it opens a file in that area). Keep this file short: every tool loads
all of it at the start of every session, Codex stops reading at 32 KiB, and
`TurboCloudAiGovernanceCheck` fails past that. A rule a fix teaches goes in the `docs/rules/` page
for its area, and comes here only when every change needs it.

## Foundational context
This repository targets the following core stack. When coding, prefer patterns compatible with these versions:
- .NET SDK `10.0.404` (from `global.json`)
- C# / BCL `net10.0`
- Orleans `10.4.2`
- EF Core `10.0.15` (on the major the target framework and the MySQL provider share: EF Core 11 needs .NET 11)
- MySQL provider `Microting.EntityFrameworkCore.MySql` `10.0.14` (Pomelo's successor; Pomelo stopped at EF Core 9)
- SuperSocket `2.1.0`

## Rules by area
Before editing, read the page for what you touch:

| You touch | Read |
|---|---|
| A grain (`Turbo.*/Grains/**`, `Turbo.Primitives/**/Grains/*.cs`) or a call from one grain to another | `docs/rules/grains.md` |
| Furni logic, teleporters, temporary furni, avatar effects, pets, bots, the room tick (`Turbo.Rooms/**`) | `docs/rules/room-objects.md` |
| Wired boxes, chests, variables, variable fx (`Turbo.Rooms/Wired/**`, `Turbo.Rooms/Object/Logic/Furniture/Floor/Wired/**`) | `docs/rules/wired.md` |
| Inventory sections, unseen items, badges, trading | `docs/rules/inventory.md` |
| A new snapshot, enum, DTO, extra data section, entity mapping or constant | `docs/rules/types-and-constants.md` |
| Logic two or more packet handlers share | `docs/rules/handlers.md` |
| Hand items, vending, asset discovery or jobs, themes, AI continuations | `docs/rules/host-features.md` |
| A test (`Turbo.Tests/**`) | `docs/rules/testing.md` |
| Revision parsers and serializers (`Turbo.Revisions/**`) | the packet addition checklist in `Turbo.Revisions/README.md` |
| A feature with a page of its own (groups, permissions, Builders Club, settings...) | that page, listed in `docs/documentation.md` |

Read only the pages for what you touch, once per session. Before finishing any change, go through
*Keeping the codebase from drifting* below. `docs/rules/drift.md` has each of those rules with the
mistake behind it, and many more; it is long, so grep it for the area you touched rather than
reading it whole.

## Priority order
1. Build and quality checks in repo files (`Directory.Build.props`, `Directory.Build.targets`, `.editorconfig`)
2. `CONTEXT.md` architecture and placement boundaries
3. This file and the `docs/rules/` page for the area
4. Existing neighboring code conventions in the target folder

## Required standards
- Target framework/tooling: `.NET 10` pinned via `global.json`.
- Keep C# formatting compatible with repo quality gates (`dotnet csharpier check`, `dotnet format`).
- Follow `.editorconfig` naming/style preferences.
- Keep diffs focused and minimal; avoid unrelated refactors.
- Grain contract changes must pass the Orleans compatibility analyzer. Follow
  `docs/orleans.md` to regenerate and review the owning project's `OrleansContracts.txt`;
  never refresh baselines merely to silence diagnostics, and preserve retired history.
- Avoid introducing new dependencies unless required by the task.

### Types and constants
The short form of `docs/rules/types-and-constants.md`:
- One public type per file, under the domain it belongs to. A snapshot lives in
  `Turbo.Primitives/<Domain>/Snapshots/<Name>Snapshot.cs`, never inside a composer, message,
  grain or service file.
- A record stored as a JSON section of a furniture item's extra data lives in
  `Turbo.Primitives/Furniture/ExtraData/` and is read through `FurnitureExtraDataSections.Read`.
- A database row becomes a snapshot through an extension on the entity in
  `Turbo.Database/Extensions/<Domain>EntityExtensions.cs`; whatever the row does not hold is a
  parameter. A derived snapshot takes its base in a `[SetsRequiredMembers]` constructor
  (`: base(info)`) instead of re-listing the base's fields.
- A value the protocol or domain defines is an enum in `Turbo.Primitives/<Domain>/Enums/`, and the
  composer or snapshot field has the enum's type. Related identifiers the client sends are one
  shared static class; anything an operator may tune is a config option. No scattered literals or
  `const` fields, and no unit-conversion constants: convert through `TimeSpan`.

## Behavioral rules for generated code
- Match local conventions in the files you touch.
- Prefer deterministic handlers/services with clear guard clauses.
- Preserve cancellation and async flow where it already exists.
- Handle failure paths explicitly; do not ship happy-path-only changes.
- Avoid dead code, unused allocations, and broad catch blocks that hide errors (see *Grains*).
- For revision compatibility work, prefer restoring/adding missing incoming message contracts in `Turbo.Primitives/Messages/Incoming/**` before mutating serializer/composer payload behavior.
- Do not alter serializer/composer behavior by replacing real payload writes with placeholder constants (for example, unconditional `WriteInteger(0)`) unless explicitly requested.
- If work references `Revision<id>` parsers/serializers, edit `Turbo.Revisions/Revision<id>/**` in `turbo-cloud`.

## Grains
The short form of `docs/rules/grains.md`. The runtime patterns (parallel grain calls, batched
writes, timer flushes, limits from config, one grain per responsibility, grains sending their own
composers, no direct database writes to grain-owned state) are in `CONTEXT.md`, *Grain runtime
patterns*.
- Never swallow an exception: `catch (Exception ex)` and log it through the grain's
  `ILogger<IGrainInterface>` with a structured template naming the ids. `OnActivateAsync`
  hydration logs with the grain key and rethrows; each `OnDeactivateAsync` step has its own
  try/catch so one failure does not skip the rest.
- Every grain has the same shape: dependencies, `_state`, modules, runtime handles, the key
  property, then a classic (not primary) constructor, `OnActivateAsync`, `OnDeactivateAsync` and
  the interface methods. In-memory domain state lives in `internal sealed class <GrainName>LiveState`,
  and the grain key is read once, in the constructor, into it.
- Buffered state is flushed on a timer and in `OnDeactivateAsync`, bounded by a configured limit.
  A failed flush re-queues, logs and gives up; it never loops while the database is down.
- Timers use the static callback form
  (`this.RegisterGrainTimer<object?>(static async (self, ct) => await ((TGrain)self!).TickAsync(ct), this, dueTime, period)`),
  use the token the timer passes, and are disposed before the final flush.
- Every type that crosses a grain call carries `[GenerateSerializer]` and an `[Id(n)]` on each
  member, never renumbered; composers and snapshots are also `[Immutable]`, events are not. A
  missing attribute fails at runtime only.
- Grain implementations are `internal sealed`; only the interface, in
  `Turbo.Primitives/<Domain>/Grains/`, is public. `RoomGrain` and every type `AssemblyExplorer`
  discovers (room object logic, wired variables, packet handlers) stay public: an internal one
  compiles and registers nothing.
- Every asynchronous grain interface method takes a `CancellationToken ct` as its last parameter.
- Grains are not reentrant. Before adding an awaited call from grain A to grain B, check that
  nothing B awaits leads back to A. A room operation that must close a session returns a result,
  and `RoomService` calls the presence. A presence method another grain awaits is an
  `[AlwaysInterleave]` tell that never awaits a grain. Two grains of one type talk only through
  interleaved tells, and a grain never calls itself through a reference (guard `target == self`).
  Never mark a grain `[Reentrant]` to make a chain work.

## Packet handlers
- Keep packet handlers orchestration-only:
  - validate input
  - call grains through canonical grain-factory access patterns
  - map snapshot data to outgoing composers
- Do not query database contexts or repositories directly from packet handlers.
- A packet handler runs only for a session with a signed-in player: the pipeline (`SignInGate`)
  drops anything else before the handler, so a handler does not open with
  `if (ctx.PlayerId <= 0) return;`. A handler for
  what the client sends before it logs in carries `[AllowAnonymous]` and is added to
  `AllowAnonymousHandlerTests` (`docs/permissions.md` §11).
- Keep persistence access in grains/services/providers that own domain state.
- Do not use ad-hoc grain key strings in handlers when extension-based access exists.
- Do not add silent `catch` blocks in handlers.
- When snapshot fields are available, map them into composer payloads instead of TODO placeholders.
- For incoming message records in `Turbo.Primitives/Messages/Incoming/**`, keep mandatory fields explicit (use `required` where appropriate) instead of default-fallback contracts.
- What two or more handlers do around their grain call goes in one
  `internal static class <Thing>Extensions` in their folder. Reuse the existing ones first
  (`docs/rules/handlers.md` lists them).

## Session and room routing
- Connection/session lifecycle starts in gateway flow; do not duplicate session registration in handlers.
- Post-SSO session attachment goes through `PlayerPresenceGrain` (one active presence grain per player id).
- There are exactly three ways to send a composer; pick by who it is for:
  - **The session that sent the packet being handled**: `ctx.SendComposerAsync(composer, ct)` in
    the handler. It is a reply to that connection and needs no grain hop.
  - **A player** (someone else, or anything a grain, module, logic class or service pushes):
    `grainFactory.SendComposerToPlayerAsync(playerId, composer, ct)`, or
    `SendComposerToPlayersAsync` for several. These are the `GrainFactoryExtensions` wrappers
    over the presence grain, which fans out to the player's sessions. Do not spell out
    `GetPlayerPresenceGrain(id).SendComposerAsync(...)` and do not add a local
    `SendToPlayerAsync` helper; five of those existed before they were folded into the
    extension. The list overload (`presence.SendComposerAsync(composers, ct)`) stays a direct
    presence call, because it is a batch for one player.
  - **Everyone in a room**: `RoomGrain.SendComposerToRoomAsync` (the room stream), or
    `RoomGrain.SendComposerToRoomAndForget` from room code that must not wait on it (the tick, a
    broadcast made once the answer is known). The second is the first with `LogAndForget`, not
    another route; fifteen call sites were spelling the log line out. A "local send helper" is
    one that wraps a *player* send or reaches the presence directly — those stay forbidden.
- **Sending a player to another room is `IGrainFactory.ForwardPlayerToRoomAsync`**, from a
  handler, a grain or a wired box alike; nothing else builds `RoomForwardMessageComposer`. The
  presence owns both halves of a forward — how the player will arrive (the pending entry a
  teleporter or room network names) and the queue the forward goes out on — so
  `IPlayerPresenceGrain.ForwardToRoomAsync` records the entry and queues the forward in one
  interleaved call. Before, the wired teleport made two grain calls that had to be chained to
  stay in order, and four handlers sent the forward themselves without touching the entry, so
  an earlier furni's entry could colour a later plain forward to the same room. Pass a
  `RoomEntrySnapshot` only when a furni decides how they land; the overload without one records
  a plain entry.
- Do not send directly to raw sockets/session transports from packet handlers.
- Active-room membership/discovery belongs to `RoomDirectoryGrain`; do not bypass it with ad-hoc room tracking.
- Grain lifetime remains Orleans-managed by default; use `[KeepAlive]` only for explicitly justified directory/manager grains.

## Admin API routes
- Every admin route declares who may call it (`RequirePermission`, `RequirePermissionToChange`,
  or a note: `PanelOnly`, `ViewIsEnough`, `ChecksInHandler`, each with its reason); the session
  filter checks it before the handler runs (`docs/admin-panel.md`, *How a route says who may call
  it*). A handler does not check a node to decide whether it may run: a route whose author forgot
  that check was open to anyone with `admin.panel`, and the same action needed different nodes
  in different files. `AdminAccessGuardTests` reads every route and fails on one that declares
  nothing.

## Tests
- A change to behaviour comes with a regression test in `Turbo.Tests` that fails without the
  change and passes with it. Drive the entry point that production uses (the grain or module
  method, or client bytes through `PacketHarness`) and assert what a player or the client would
  observe, not private fields. `Turbo.Tests/Support` builds rooms, grains, packet round-trips and
  databases without a silo; `docs/patterns/UnitTestPattern.cs` shows the shape.

Before writing one, read `docs/rules/testing.md`: what each `Turbo.Tests/Support` helper
(`RoomHarness`, `WiredRoom`, `GrainHarness`, `PacketHarness`, `Fakes`...) gives you.

### Verification loop (once each, in this order)
1. `dotnet test Turbo.Tests/Turbo.Tests.csproj --filter <YourTestClass>` until it passes.
2. Show it catches the bug: save `git diff -- <the production files you changed>` to a patch
   outside the repository, `git checkout -- <those files>`, run step 1 again and expect it to
   fail, then `git apply` the patch. Do not use `git stash`: the stash list is shared by every
   session and worktree on the machine, and a parallel agent's `pop` can take yours.
3. At the end, one `TurboCloudQualityGate` build and one full `dotnet test`.

A change to markdown alone (root `*.md`, `docs/**/*.md`, `.claude/rules/*.md`) needs no build
and no test run; CI skips them for it too (`scripts/ci_scope.py`). When it moves or renames a
page or a heading, build the docs site instead, which fails on a dead link or anchor:
`npm ci` then `npm run build` in `docs-site/`.

## Keeping the codebase from drifting
Most drift here came from adding a second copy of something that already existed. Check a change
against this list before finishing it; `docs/rules/drift.md` has each rule with the mistake that
taught it, and many more.
- **Search before adding.** A config key, constant, helper, event or grain method may already
  exist. An unused one is wired up or deleted, not given a sibling.
- **One way to do a thing.** When two members do the same job, collapse them in the same change and
  move every caller. Aliases kept "for compatibility" inside this repository are drift.
- **Sibling features share a shape.** When one inventory section, wired box kind or pet/bot module
  gets a capability, give it to its siblings or say why not.
- **Batch across grain boundaries.** Pass the whole set, group by target grain, and run
  independent targets with `Task.WhenAll`; a grain call per item in a loop is a bug in waiting.
- **No silent catch, no blocking wait, no discarded task.** `_ = SomethingAsync(...)` is `.Ignore()`
  by another name: await it, return it, or end it with `.LogAndForget(logger, "what it was doing")`.
- **Two classes that differ in one line share a base**, and a second private method with the same
  body is a copy.
- **A composer that mirrors a snapshot carries the snapshot.**
- **A claim about the client is checked in the client**, and so is a placeholder on the wire: read
  the Flash client's AS3 source before writing a behaviour or a `0` into code or a comment.
- **Ask whose it is before where it fits.** New state, a setting or a method goes to the system
  that owns the concept, not to the one that needed it first.
- **Remove what a change orphans**: the setting nothing reads, the interface member with no caller,
  the using, the appsettings key. A feature that moves takes its leftovers with it.
- **A name in a comment is checked like a name in code**, and a summary says what the method does now.
- **An option is read through its config class, never by key name.** A new option carries the
  default it ships with and a summary; `required` is only for an option with no sensible default.
- **A packet handler takes a primary constructor**; a grain takes a classic one.
- **A cache that answers "no" is invalidated when the answer becomes "yes"**, and every field of a
  cached copy needs an invalidation path.
- **Two `SaveChangesAsync` calls are two chances to half-finish.** Money that moves before the
  thing it buys exists is refunded through `IGrainFactory.RefundAsync` when the thing fails.
- **Every path to a state checks the same limit and the same permission.** When you find a check in
  one path, list every other way to reach that state.
- **A count the client sends is read with `PopList` / `PopCount`, never `PopInt`.**
- **The room tick never waits on a database write**, and room code reads time from the room's
  clock (`NowMs()`), never the machine's.
- **Zero warnings is the baseline, and an incremental build hides them.** Build to a scratch
  `-p:OutDir` to see the whole tree's output; unused usings show up only in a real build.
- **A rule has one home**: this file, `CONTEXT.md`, or the `docs/rules/` page for its area. When a
  rule changes, grep all three for the old wording; when two disagree, find which one the code
  follows and delete the other.
- **A rule a fix teaches is written in the same change**, in the `docs/rules/` page for its area.

## Required validation before completion
```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
dotnet test Turbo.Tests/Turbo.Tests.csproj
```

## Definition of done for AI changes
- All modified files match nearby patterns and contract rules.
- Quality gates pass with no new warnings introduced by the change.
- Architecture invariants for touched areas are explicitly confirmed in PR.
- Edge/failure behavior is addressed for logic changes.
- A behaviour change has a regression test in `Turbo.Tests` that fails without it (*Tests* above).
- Any context-rule updates needed by the change are included in the same PR.
