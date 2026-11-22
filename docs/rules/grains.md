# Grain Rules

- [Writing a grain](#writing-a-grain)
    - [Never swallow exceptions silently](#never-swallow-exceptions-silently)
    - [Every grain has the same shape](#every-grain-has-the-same-shape)
    - [Activate and deactivate the same way everywhere](#activate-and-deactivate-the-same-way-everywhere)
    - [Register timers one way](#register-timers-one-way)
    - [Attribute every type that crosses a grain call](#attribute-every-type-that-crosses-a-grain-call)
    - [Declare grain implementations internal sealed](#declare-grain-implementations-internal-sealed)
    - [Grain method signatures](#grain-method-signatures)
    - [Parallelize independent grain calls](#parallelize-independent-grain-calls)
    - [Do not repeat identical grain calls in loops](#do-not-repeat-identical-grain-calls-in-loops)
    - [Batch DB operations](#batch-db-operations)
    - [Use timer-based flush for housekeeping writes](#use-timer-based-flush-for-housekeeping-writes)
    - [Do not hardcode limits in grains](#do-not-hardcode-limits-in-grains)
    - [Use tracked deletes for atomicity](#use-tracked-deletes-for-atomicity)
    - [Replace .Ignore() with a LogAndForget helper](#replace-ignore-with-a-logandforget-helper)
    - [Bound session/history collections](#bound-sessionhistory-collections)
    - [One grain per responsibility — isolate heavy I/O](#one-grain-per-responsibility--isolate-heavy-io)
    - [Use grain boundaries for thread safety](#use-grain-boundaries-for-thread-safety)
    - [Grains orchestrate their own outbound communication](#grains-orchestrate-their-own-outbound-communication)
    - [Do not mutate the database directly for grain-owned state](#do-not-mutate-the-database-directly-for-grain-owned-state)
- [Grains are not reentrant: side effects on other grains that call back go in the service](#grains-are-not-reentrant-side-effects-on-other-grains-that-call-back-go-in-the-service)

How an Orleans grain is written in Turbo, and how grains call each other without deadlocking.
Every one of these mistakes has shipped and caused real issues. [`CONTEXT.md`](../../CONTEXT.md)
lists the runtime patterns as one-liners (*Grain runtime patterns*) and
[`AGENTS.md`](../../AGENTS.md) keeps the short form; this page is the full rule and the reason
behind it. [Grains & Orleans](../grains.md) is the tour for plugin authors.

## Writing a grain

### Never swallow exceptions silently
Every bare `catch { }` hides a real bug path. Always use `catch (Exception ex)` and log it.
If a cross-grain notification fails silently, state goes asymmetric and nobody knows why.
- **Required**: inject `ILogger<T>` into every grain that does cross-grain calls or DB work, typed
  by the grain interface (`ILogger<IPlayerGrain>`), and reach it from that grain's modules and
  systems rather than giving them their own.
- **Forbidden**: bare `catch { }`, `catch (Exception) { }` without logging, and `Console.WriteLine`
  in place of the logger.
- Log with structured templates and the identity the entry is about — `_logger.LogError(ex, "Failed
  to remove item {ItemId} from room {RoomId}", itemId, _state.RoomId)` — never string interpolation,
  so entries can be searched by id.
- Pick the level by who is at fault: `LogError` for a failure of ours, `LogWarning` for a rejected
  or impossible request (insufficient balance, unknown id), `LogDebug` inside per-tick loops where
  error-level logging would flood.
- `OnActivateAsync` hydration: catch, log with the grain key, then rethrow. Activation must still
  fail, but never namelessly.
- `OnDeactivateAsync`: isolate each step in its own try/catch and log. One failing step must never
  skip the ones after it — a failed flush still has to release the grain's registrations.

### Every grain has the same shape
A reader who knows one grain should be able to find their way in any other. `PlayerGrain`,
`PlayerNavigatorGrain` and `RoomPersistenceGrain` are the references.
- **Members, top to bottom:** injected dependencies (readonly), `_state`, modules, then runtime
  handles (timers, stream subscriptions, the session observer), then the key property, then the
  constructor, `OnActivateAsync`, `OnDeactivateAsync`, and the interface methods.
- **Constructor:** a classic constructor, never a primary one. Most grains need a body (the
  state is created with the grain key, modules take `this`), and a grain that captured
  primary-constructor parameters used them directly in its methods while its neighbours used
  `_fields`. One form everywhere ends that.
- **Parameter order:** `IDbContextFactory<TurboDbContext>`, `IOptions<...>` config, `IGrainFactory`,
  domain providers and services, `EventSystem`, and `ILogger` last. The logger is typed on the grain
  interface (`ILogger<IPlayerGrain>`), not the class. DI resolves by type, so the order is only
  for the reader; keep it anyway.
- **Live state:** everything the grain holds in memory about its domain (collections, snapshots,
  dirty flags, write buffers, counters) lives in `internal sealed class <GrainName>LiveState`, in
  `<GrainName>LiveState.cs` beside the grain, held as `private readonly <GrainName>LiveState _state`.
  Members are properties: `{ get; }` for collections, `{ get; set; }` for values, and a comment
  where the meaning is not obvious. Loose `_dictionary` fields on the grain are drift. What stays
  on the grain is what is not data: dependencies, config, modules, and handles that must be
  disposed (timers, stream subscriptions, observers).
- **The grain key** is read once, in the constructor, into the state
  (`_state = new() { PlayerId = this.GetPlayerId() }`, `RoomId = this.GetRoomId()`), and code reads
  `_state.PlayerId`. Do not call `this.GetPlayerId()` or `this.GetPrimaryKeyLong()` through the
  body, and never read a key through the wrong helper (the raffle grain read its series id with
  `GetPlayerId()`). A singleton grain's state carries no key; a stateless grain
  (`CatalogPurchaseGrain`) has no state class and reads its key where it needs it.
- **Timer fields** are named for what they drive (`_flushTimer`, `_dirtyItemsTimer`), never `_timer`.
- **A flush used in `OnDeactivateAsync` must be able to give up.** If a failed write re-queues its
  rows, a `while (pending > 0) await FlushAsync()` loop never ends while the database is down.
  Have the flush report whether it made progress and stop on the first failure.
- **Module files hold one concern.** A partial module file is named for what it covers
  (`RoomPetModule.Products.cs` is things used on a pet). Placement, pick-up and returning to the
  owner are the module's core and live in the main file, in the same place for sibling modules
  (`RoomPetModule.cs`, `RoomBotModule.cs`).

### Activate and deactivate the same way everywhere
- A grain that owns database-backed state hydrates it in `OnActivateAsync`, inside a try/catch that
  logs with the grain key and rethrows. Do not hydrate bare: a silent activation failure surfaces
  later as empty state.
- Lazy hydration is allowed only where eager loading would be wasteful (`InventoryGrain` loads
  furniture on first use, because the grain is also activated for cheap lookups). Say so in a
  comment on the class, so the exception reads as a decision rather than an omission.
- Every grain's class comment states how its state reaches the database: write-through
  (`PlayerWalletGrain`, `PlayerWardrobeGrain`, `PlayerMessengerGrain`) or buffered-and-flushed
  (`PlayerSettingsGrain`). This is what tells a reader whether a missing `OnDeactivateAsync` is
  correct.
- Buffered state is flushed on a timer *and* in `OnDeactivateAsync`, and the buffer is bounded by a
  configured limit so a database outage cannot grow it without end.
- A failed flush re-queues what it dropped (up to that limit) and logs; it never throws out of
  deactivation.
- Do not leave `OnActivateAsync`/`OnDeactivateAsync` overrides that only `return Task.CompletedTask`.
  An empty override says nothing the base class does not already do.

### Register timers one way
- Use the static callback form so the timer never captures the grain's fields or an outer token:
  `this.RegisterGrainTimer<object?>(static async (self, ct) => await ((TGrain)self!).TickAsync(ct), this, dueTime, period)`.
- The callback body belongs in a named method on the grain, not inline in the registration.
- Use the token the timer passes. Never capture the `CancellationToken` from `OnActivateAsync`: it
  covers activation only, and the timer outlives it.
- Keep the returned `IDisposable` in a field and dispose it in `OnDeactivateAsync`, before any final
  flush, so a tick cannot race deactivation.
- A tick that can fail catches, logs and keeps the schedule alive; one bad tick must not stop the
  grain from ticking again.

### Attribute every type that crosses a grain call
- Orleans deep-copies each argument and return value of a grain method. A type without
  `[GenerateSerializer]` throws `CodecNotFoundException` the first time it crosses a grain call —
  at runtime only; the build says nothing.
- New composer: `[GenerateSerializer, Immutable]` on the record and `[Id(n)]` on every member,
  numbered from zero and never renumbered afterwards. `Immutable` is correct for composers because
  they are built once and sent; it lets Orleans skip the copy.
- Same for snapshots and any DTO named in a grain interface. Events keep `[GenerateSerializer]`
  without `Immutable`, since handlers mutate them (`PlayerChatEvent` rewrites `Text`).
- A composer that is only ever sent from inside the grain that built it still needs this: the next
  caller to route it through `IPlayerPresenceGrain.SendComposerAsync` crosses a proxy.

### Declare grain implementations `internal sealed`
- A grain implementation is an implementation detail: only its interface is public. Declare it
  `internal sealed` (`internal sealed partial` when split across files) and keep the interface in
  `Turbo.Primitives/<Domain>/Grains/`.
- The exception is `RoomGrain`, which stays public, and the reason is on the class. Types discovered
  by `AssemblyExplorer` (`IRoomObjectLogic` implementations, `IWiredInternalVariable`
  implementations) must be public — it skips non-public types — and those name `RoomGrain` in their
  constructors and protected fields, so it cannot be narrower than they are.
- Packet handlers (`IMessageHandler<T>`) are discovered the same way and stay public for the same
  reason.
- Before narrowing any type's visibility, check whether it is reflection-discovered. Making one
  internal still compiles; it just registers nothing at startup, which no build or test catches.

### Grain method signatures
- Every asynchronous grain interface method takes a `CancellationToken ct` as its last parameter,
  including simple getters. Callers pass the token they were given, or `CancellationToken.None`
  for fire-and-forget calls.
- Take the grain key once, through `this.GetPlayerId()` / `this.GetRoomId()`, instead of casting
  `this.GetPrimaryKeyLong()` at each use site.

### Parallelize independent grain calls
When checking status on N grains (e.g. online status for a friend list), do not `await` each one in a `foreach`.
Grain calls to different grains can run concurrently with `Task.WhenAll`.
- Sequential = O(n) round-trips. Parallel = O(1) wall time.
- Apply everywhere: activation hydration, search results, batch accept/deny.

### Do not repeat identical grain calls in loops
If a grain method calls its own player's `GetSummaryAsync` inside a loop, hoist the call before the loop.
Same result every iteration = wasted round-trips.

### Batch DB operations
Do not loop `ExecuteDeleteAsync` per entity. Use a single `WHERE ... IN (...)` query.
Same for composer fan-out: collect all updates, send once.

### Use timer-based flush for housekeeping writes
Follow the `RoomPersistenceGrain` pattern: queue dirty state, flush with `RegisterGrainTimer` on interval, and flush on `OnDeactivateAsync`.
Do not issue per-event DB writes that block the grain turn.

### Do not hardcode limits in grains
Magic numbers like `Take(50)`, `Take(20)`, or `maxIgnoreCapacity = 100` are config options (see
[Constants and magic values](types-and-constants.md#constants-and-magic-values)). A 10,000 user hotel needs different tuning than a 10 player dev
server.
- The grain that enforces a limit reads it: it takes `IOptions<TConfig>` of its module's config
  class in its constructor and keeps `.Value` in a field (`PlayerMessengerGrain` reads
  `PlayerConfig.MessengerNormalFriendLimit`, `BadgeLeaderboardGrain` reads `BadgeConfig`).
- A limit is not a parameter of a grain interface method and a handler does not read it from
  `IConfiguration` to pass along. `AGENTS.md` used to say the opposite, and it lost: a limit that
  arrives as an argument is only as good as every caller, a plugin calling the grain skips it
  altogether, and the three handlers that still injected `IConfiguration` for it never read it.
- What does arrive as an argument is a number the *client* chose (a page size, a chunk index).
  The grain clamps it to the configured limit before using it
  (`BadgeLeaderboardGrain.GetLeaderboardAsync`).

### Use tracked deletes for atomicity
`ExecuteDeleteAsync` commits immediately and bypasses the EF change tracker.
If a delete + insert must succeed or fail together, use `FirstOrDefaultAsync` + `Remove` so both go through one `SaveChangesAsync`.

### Replace .Ignore() with a LogAndForget helper
Orleans `.Ignore()` makes cross-grain failures invisible. Use a `LogAndForget` extension that calls `ContinueWith(OnlyOnFaulted)` to log the exception.
Still fire-and-forget, but failures are visible in production logs.

### Bound session/history collections
Any in-memory collection that grows per-message (e.g. conversation history) must have a configurable cap.
Without a cap, long-running sessions leak memory.

### One grain per responsibility — isolate heavy I/O
Each major domain component should operate in its own grain. When a grain needs heavy I/O (DB writes, persistence flushes), delegate that work to a dedicated secondary grain so it does not block the primary grain's turn.
- Example: `RoomGrain` delegates furniture saves to `RoomPersistenceGrain`. The room grain stays responsive while persistence queues and flushes.
- Do not combine domain logic and persistence flushing in the same grain.
- The same holds for reads. A hotel-wide singleton that other grains await on their hot path (a
  directory) answers from memory; a query per request goes in a grain of its own. The badge
  leaderboards were first written into `BadgeDirectoryGrain`, where a grouped query over every
  badge row held up every badge list, profile and room entry in the hotel until it returned;
  they are now `BadgeLeaderboardGrain`.

### Use grain boundaries for thread safety
Orleans grains are single-threaded by design. Use this for concurrency-sensitive operations by giving each user their own grain for the operation.
- Example: each player gets a `PurchaseGrain` so catalog purchases are serialized per-player with no locks needed.
- Example: limited-edition items should use a dedicated grain (e.g. `LimitedItemGrain`) so concurrent buyers are safely serialized.
- Do not add manual locking (`lock`, `SemaphoreSlim`) inside grains — that fights the actor model.

### Grains orchestrate their own outbound communication
When grain state changes (e.g. wallet balance updates), the grain itself builds the composer and
sends it: `grainFactory.SendComposerToPlayerAsync(playerId, composer, ct)`, or one batch with
`GetPlayerPresenceGrain(id).SendComposerAsync(composers, ct)` when it sends several to the one
player. The caller that triggered the change does not pass or send the composer — the grain owns
that responsibility.
- **Correct**: handler calls `grain.UpdateWalletAsync(...)` → grain updates state → grain sends its balance composer.
- **Wrong**: handler calls `grain.UpdateWalletAsync(...)` → handler builds composer → handler sends composer to player.
- **Also wrong**: grain calls a presence method (`OnCurrencyUpdateAsync`) whose only job is to build
  that composer. The presence is the transport, not the place that knows every domain's packets.
  It kept fifteen such pass-throughs (balances, inventory adds and removes, badges, friend
  requests, block and ignore lists, the figure, the friend-list update) and five "open a list"
  flows that only fetched from the owner and sent; each owner sends its own now, and the
  presence interface stopped growing with every feature.
- **When a presence method is right**: only when the presence's own state decides what is sent or
  where — the active room (`OnSelectedBadgesChangedAsync`, `OnPlayerUpdatedAsync`,
  `OnHabboClubChangedAsync`, `OnControllerLevelUpdatedAsync`), the pending entry
  (`ForwardToRoomAsync`). Such a method is a tell: interleaved, awaiting no grain.
- **A list the client asks for** is answered by the handler from a snapshot with
  `ctx.SendComposerAsync` when that is all it takes (the wardrobe). When the packet needs the
  owner's settings (fragment sizes, limits) or the owner also pushes it, the owner has a
  `Send…Async` the handler calls: `IInventoryGrain.SendFurnitureInventoryAsync` / pets / bots,
  `IPlayerBadgeGrain.SendBadgeInventoryAsync`, `IPlayerMessengerGrain.SendInitAsync`. Split a list
  into fragments with `ComposerFragments.Build`.

### Do not mutate the database directly for grain-owned state
Grains may hold cached or in-memory state that will not reflect direct DB changes. All mutations to grain-owned data must go through the grain's methods, even when the player is offline.
- If a grain uses `[PersistentState]`, state is hydrated from the configured store (not DB) on activation. Direct DB edits will be overwritten by stale store data.
- Admin tools and external systems must call grain methods, not issue raw SQL/DB updates, for data that grains own.

## Grains are not reentrant: side effects on other grains that call back go in the service
- `RoomGrain` and `PlayerPresenceGrain` call each other. A room grain that awaits a presence grain
  which in turn calls the room (for example `ClearActiveRoomAsync` → `RemoveAvatarFromPlayerAsync`)
  deadlocks, because grains are single-threaded and non-reentrant.
- So a room operation that must close a player's session (kick, ban, delete) is split: the grain
  validates and removes the avatar and returns a result; `RoomService` then calls the presence
  grain. Entry, doorbell and close already work this way; follow them.
- Closing the session of a player the room has already removed is
  `IPlayerPresenceGrain.OnRemovedFromRoomAsync(roomId, kicked)`. It never calls the room back, so
  it is the one eviction call that is safe from inside the room grain; `RoomService` uses the same
  call for kicks, bans and room deletion. Code that runs in the room tick (a wired kick) does not
  await it: it goes out with `LogAndForget`, because the presence may itself be waiting on the room.
- **The presence grain is the hub, so its methods come in two kinds** (the rule is written on
  `IPlayerPresenceGrain` too). Nearly every grain awaits the presence, and the presence awaits
  the room, the player, the messenger and the inventory — so it closes a cycle with almost
  anything. A deadlock sweep found cycles through it with the room, the player grain, the
  messenger, the inventory, the group grain, Builders Club and the wallet, all live.
  - **Tells** — sends, `On*` notifications, reads of a field (`GetActiveRoomAsync`),
    `SetPendingRoomEntryAsync`. `[AlwaysInterleave]` on the interface, and they **never await a
    grain**: anything they pass on to a room or the messenger goes out with `LogAndForget`
    (`OnPlayerUpdatedAsync` used to await the room, which made player → presence → room → player
    a three-grain cycle through respect and mannequins). Any grain may await a tell, even while
    the presence is waiting on that grain.
  - **Flows** — entering and leaving a room, the session lifecycle. Not
    interleaved; they await other grains; only handlers and the presence's own session drive
    them. No grain awaits a flow.
  - A new presence method another grain will await is a tell. If it needs to ask something, it is
    a flow, and the grain that wanted it tells instead.
  - Interleaving is safe for a tell because it does its work in synchronous stretches: it
    enqueues a composer or sets a field between awaits, it never holds an invariant across one.
    A method that checks, awaits, then acts on what it checked is not a tell, whatever it is
    called.
- **Two grains of the same type talk to each other more than any other pair.** Two friends both
  message, both log in, both accept, both remove — and every one of those was an awaited call
  from one messenger to the other, so two people doing the same thing at the same moment
  deadlocked both. A call between two instances of one grain type is an interleaved,
  memory-only tell (`OnFriendAdded`, `ReceiveMessage`, `CanBeAddedBy`); the side that starts a
  change does the database work for both, in one save.
- **A grain never calls itself through a reference.** `GetPlayerMessengerGrain(self).X` inside
  the messenger waits for a turn that can only start once the current one ends. It happened on a
  friend request to your own name. Any operation that takes another player's id needs a
  `target == self` guard when the target's grain is the same type as the caller's.
- The same one-way rule holds for every helper grain of a room: `RoomTradeGrain` awaits the room,
  so the room only ever tells it things with `LogAndForget`. Before adding an awaited call from
  grain A to grain B, check that nothing B awaits leads back to A.
- Never mark a grain `[Reentrant]` to make such a chain compile. It moves the bug from a deadlock to
  interleaved state.
- **Auditing this is mechanical; do it rather than reason about it.** Every awaited grain-to-grain
  call can be listed from the source: find the `Get*Grain(...)` accessors (and
  `SendComposerToPlayer(s)Async`, which reaches the presence grain), work out which grain the file
  belongs to — a module or a `.Partial.cs` belongs to its grain — and record whether the
  statement is `await`ed or goes out with `LogAndForget`. Pairs with an awaited edge in **both**
  directions are the candidates. A sweep of the tree found three; two are now one-way.
- **The audit is at pair granularity; safety is per method.** A reported pair is not automatically
  a bug: what matters is whether the specific method A awaits can lead back to A. Read both
  methods before acting. What *is* automatic is the reverse — a pair with no awaited edge in one
  direction cannot deadlock however either side changes later, which is why turning an awaited
  notification into a `LogAndForget` is usually the cheapest permanent fix.
- **Known mutual-await pairs, and what keeps them safe:**
  - `PlayerPresenceGrain` ↔ `RoomGrain`, `PlayerGrain`, the messenger, the inventory, the group
    grain, the wallet — every one of them awaits the presence only through a tell, which is
    interleaved and awaits nothing. That is structural now, not discipline per method; it stays
    true as long as the tell/flow split above holds.
  - Messenger ↔ messenger — interleaved tells only (see above).
  - "Safe because the other side only sends composers" was the justification for Room ↔ Presence
    for a long time, and it was not true: the presence was not free to *receive* the send while
    it was itself waiting on the room. What makes an edge safe is that the callee can run the
    call *now*, which is what `[AlwaysInterleave]` says.
- **Moving the call to the caller is the wrong half of the fix.** The one-way rule says who may
  *await* whom. It does not say the owner of the state gives up publishing what changed. A group
  grain that must not await a room still tells the room, with `LogAndForget` — the group system
  spent a while with five handlers each remembering to refresh a room after calling the grain,
  which is the same bug as building a composer in a handler. Put the push in the grain that
  changed, fire-and-forget, and say in a comment that it is not awaited and why.
- **Publish from the narrowest place every path already goes through.** Eight roster operations
  (join, approve, approve-all, promote, demote, kick, leave, block) all end in
  `AddOrUpdateMemberAsync` or `RemoveMemberAsync`, so the member's own grain and their rights in
  the homeroom are refreshed there — two call sites, not eight. Four group edits all end in
  `PublishChangedAsync`. Before adding a notification, find the funnel; if there is not one, that
  is usually the thing to fix first.
