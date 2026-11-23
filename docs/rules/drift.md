# Keeping the Codebase From Drifting

[`AGENTS.md`](../../AGENTS.md) keeps a short checklist of these. This is the full list, with the
mistake behind each rule.

Most drift here came from adding the second copy of something that already existed. Before
finishing a change, check it against this list; each line is a mistake that was made and fixed.
- **Search before adding.** A config key, constant, helper, event or grain method: grep for an
  existing one first. `WiredMaxDepth` (later `WiredConfig.MaxDepth`, since removed) existed unused while a second depth setting was added beside
  it. An unused setting is a signal to wire it up or delete it, not to add a sibling.
- **One way to do a thing.** If a change makes two members do the same job (two selection
  methods, two eviction paths, two ways to build an inventory item), collapse them in the same
  change and move every caller. Aliases kept "for compatibility" inside this repository are drift.
- **Sibling features share a shape.** Inventory sections, wired box kinds, pet and bot modules:
  when one gets a capability (batching, a `GetAsync`, a delete), give it to its siblings or say
  why not. Behaviour lives in the module; grain partials forward.
- **Batch across grain boundaries.** A loop that makes a grain call per item is a bug in waiting.
  Pass the whole set (`AddFurnitureFromRoomItemSnapshotsAsync`), group
  by target grain, and run independent targets with `Task.WhenAll`. Returning furni, pets and bots
  on room deletion all follow that shape: the room lets go first, then each owner's inventory
  takes its share back concurrently.
- **One public type per file, in the folder for its kind** (see [Type placement](types-and-constants.md#type-placement)). A base class and
  its two registered subclasses are three files.
- **No silent catch, no blocking wait, no discarded task.** All three hide failures that only
  show up under load.
  - A packet handler does not wrap its body in `try { } catch (Exception) { }`: `EnvelopeHost`
    logs every packet- and event-handler failure with the handler and envelope type, and
    `PackageHandler` logs parse and context failures. A handler catches only a typed exception it
    turns into a reply (`CatalogPurchaseException` → `NotEnoughBalance`). This bullet used to
    credit `PackageHandler` alone, which never saw a handler exception: the pipeline's error
    callbacks were `(ex, env) => { }` in both registries, so every handler failure in the server
    vanished.
  - An error callback or hook defaults to logging, never to an empty lambda, and a component that
    catches on behalf of others takes an `ILogger`.
  - Never `throw` inside a `try` whose `catch` is broad. The plugin loader's "more than one plugin
    entry type" check sat inside a bare `catch` and could never fire.
  - `_ = SomethingAsync(...)` is `.Ignore()` by another name. Await the task, return it, or end it
    with `.LogAndForget(logger, "what it was doing")`. Handlers await their sends. Room code that
    must not hold up the tick (broadcasts, event publishes, wired box flashes) uses `LogAndForget`.
    A callback typed `Func<Task>` whose callers cannot await it is an `Action`: extra data, wired
    data and wired storage each discarded the task of a change callback every implementation
    finished at once, and the stuff data's callback was never set by anyone.
  - Do not use an exception as a lookup (`First` inside `try`, an index inside `try`): use
    `TryGetValue`, a bounds check or `GetIntParamOrDefault`. The only commented swallows left are
    in runtime primitives that have no logger and run during unload (`ReloadableExport`,
    `CompositeDisposable`); do not add new ones.
- **Room code names its siblings the way the grain does.** Every module, system, object logic and
  wired context derives from `RoomGrainComponent`, which exposes the grain's modules and systems
  by name: write `ObjectModule.RemoveObjectAsync(...)`, not
  `_roomGrain.ObjectModule.RemoveObjectAsync(...)`. The same goes for the grain's `State`,
  `Logger`, `GrainFactory`, `RoomConfig` and `WiredConfig`: write `State.ItemsById`, not
  `_roomGrain._state.ItemsById`. `_roomGrain` stays for the grain's other services (database,
  providers, the rarer configs). A new module or system gets its shorthand in
  `RoomGrainComponent` (the one list), and a new class that holds the room grain derives from it
  rather than keeping its own `_roomGrain` field. Static helpers that take the grain as a parameter (`WiredArea`,
  `WiredTimeZones`) are the exception.
- **The same holds for every grain that splits its work into helper classes.** A grain with
  modules holds them as named fields and gives them one `<Grain>Component` base that names what
  they share: `InventoryGrainComponent` exposes the owner (`PlayerId`), `Presence`, `Config`,
  `GetOwnerNameAsync` and the sibling modules, so an inventory module writes
  `Presence.SendComposerAsync(...)`, not `_inventoryGrain.Presence.SendComposerAsync(...)`. The room
  and the inventory are the two grains built this way today; a grain that grows its first module
  starts with its component base rather than a `_<grain>` field in the module.
- **Shared room lookups have one home.** Player id → avatar is `RoomAvatarModule.TryGetPlayer`;
  do not walk `AvatarsByPlayerId` then `AvatarsByObjectId` inline, and do not park a general
  helper in whichever module needed it first (it lived in the pet module, and trading reached
  into pets to find a player). When a second module needs a helper, move it to the module that
  owns the state before calling it.
- **Two classes that differ in one line share a base.** Before copying a class to change a
  detail, extract what stays the same and leave the detail abstract: the wired neighbourhood
  selectors (`FurnitureWiredNeighborhoodSelectorLogic`), the furni placement variables
  (`FurniturePlacementVariable`), the pet products (`FurniturePetProductLogic`) and the six wired
  save handlers (`UpdateWiredMessageHandler<TMessage>`) were each written out in full per
  variant, and the copies had already drifted (only the trigger save reported a refused save).
  When the variants cannot share a base (a wired action and a wired condition), put the shared
  lifecycle in the common ancestor behind an opt-in flag (`KeepsFurniSnapshot`).
- **Declarations repeat too.** A list or block that many boxes declare identically gets a name:
  `WiredSources.Users` / `Furni` / `PickedFurni`, `AllVariablesContext()`, `GetTargetType(...)`.
  A box spells its own list out only when it really differs.
- **A composer that mirrors a snapshot carries the snapshot.** Do not copy twenty fields from a
  snapshot into a composer in every handler (`ExtendedProfileMessageComposer { Profile = ... }`);
  the serializer reads the snapshot.
- **A second private method with the same body is a copy.** The wired mute was added as a
  duplicate of the player mute; both now go through `StoreMuteAsync`. Same for two classes that
  are identical but for the name (`PackageEncoderWs` was `PackageEncoder`).
- Scan for copies with a script over `git ls-files -co --exclude-standard`, not plain
  `git ls-files`: files that are new in the working tree are the most likely to hold them, and
  a tracked-only listing skips exactly those.
- **A bulk replace must skip the definition it points at.** When call sites are rewritten to use
  a new helper by script or search-and-replace, the helper's own body matches the pattern too.
  `RoomAvatarModule.TryGetPlayer` was rewritten to call itself this way and overflowed the stack
  the first time a player entered a room; it compiled, and no gate catches it. Exclude the
  defining file or method from the replace, then read the helper again afterwards.
- **A diff is computed, not assumed.** When the client sends what it already holds (ids with
  hashes), compare before resending; the wired variable sync resent every variable on each
  request because the hash was read and never used.
- **A claim about the client is checked in the client.** Behaviour attributed to the Flash client
  (what it sends after `CloseConnection`, what a param means) is read from the AS3 source before
  it is written into code or a comment. Where the source only has localisation keys, the
  assumption is recorded on the enum.
- **A feature that moves takes its leftovers with it.** When badges left `PlayerGrain`, the
  usings they needed stayed behind in `PlayerGrain` and `PlayerLiveState`; no gate flags an
  unused using, so read the file you removed code from once more.
- **Build output is part of the diff.** The gates fail on errors, not on warnings, so read the
  warnings for the files you touched. `GetBadgeInfoMessageHandler` awaited its two tasks again
  after `Task.WhenAll` without `ConfigureAwait(false)` and added two CA2007 warnings; read
  results into locals with `ConfigureAwait(false)` like every other handler await.
- **A new setting looks like its siblings.** A config option carries the hotel default it ships
  with (`public int X { get; init; } = 100;`) and a summary saying what it tunes. Its key goes in
  `appsettings.json` when a hotel is expected to change it; the default is what a hotel that says
  nothing gets. `required` is for an option with **no** sensible default, which therefore carries
  none and has to be in `appsettings.json` — `CryptoConfig`'s key pair is the only one in the
  repository. `AGENTS.md` used to say every option is `required`, generalised from `PlayerConfig`,
  and lost: two hundred options across `RoomConfig`, `WiredConfig`, `PetConfig`, `NavigatorConfig`
  and the rest carry defaults instead, and `required int X { get; init; } = 500` says both things
  at once. It is also a promise the binder does not keep: `services.Configure<T>(section)` builds
  the object by reflection, so a missing key is not an error — ten of `PlayerConfig`'s twenty-three
  `required` options had no key at all and nothing noticed.
- **A rule has one home.** `CONTEXT.md` and `.github/copilot-instructions.md` still sent
  revision work to the plugin repo after it moved to `Turbo.Revisions/`, because they kept
  copies of rules `AGENTS.md` had changed. The tool files (`CLAUDE.md`,
  `.github/copilot-instructions.md`) now only point at `AGENTS.md` and `CONTEXT.md`. A rule is
  written in one of those two or in the page under `docs/rules/` for its area, never in two of
  them; when one changes, grep all three for the old wording in the same change.
- **A rule that lost is deleted, not left beside the winner.** `AGENTS.md` told grains to take
  limits from handlers in one section and from their module config in another; new code picked
  either. When two rules disagree, find which one the code follows, delete the other wherever it
  is written, and remove what the dead rule left in the code (the unused `IConfiguration`
  injections).
- **A field nobody reads is a question, not padding.** `WiredSetObjectVariableValue`'s last int
  was named `ReferenceRoomId` by guess and never read. It is the operation (set, create,
  delete), so deleting a variable from the wired menu set it to zero instead, and nothing
  failed. When a parser pops a field the handler ignores, find the composer's call sites in
  the AS3 and see what the client puts there. Likewise a header constant with no parser
  (`WiredDeleteAllVariableHoldersMessageEvent`) is a feature the client has and the server
  does not; `scripts/packetgap.py` lists them.
- **A placeholder on the wire is checked in the client.** `BadgesRank = 0` looked harmless and
  was sent for every avatar; the client draws any rank of zero or more, so everyone was "#0".
  Before leaving a `0` or `// TODO` in a snapshot a serializer writes, read what the client does
  with that value and send its "nothing" (`BadgeRanks.NONE`) instead.
- **Ask whose it is before where it fits.** New state, a new setting or a new method goes to the
  system that owns the concept, not to the one that needed it first (see [System boundaries](wired.md#system-boundaries-wired-is-a-user-of-the-room-not-a-part-of-it)).
  A `Wired` prefix on something in a general class, or a general module checking for a wired
  type, is the sign it landed in the wrong place.
- **A count another domain owns is asked of its owner.** The LTD raffle weighed a buyer by
  counting their badge, room and furni rows itself, beside a navigator that already cached the
  room count and a badge grain that holds the badges in memory. It asks
  `IPlayerBadgeGrain.GetBadgeCountAsync`, `INavigatorProvider.GetRoomCountForOwnerAsync` and
  `IInventoryGrain.GetOwnedFurnitureCountAsync` now; when the owner has no such read, give it one.
- **A rule written in a change is kept by that change.** The variable fx section said every
  client table lives in `VariableFxStyles` while the team colours sat in the system; the
  logic rules said state values live in `Turbo.Primitives/Furniture/` while the rentable space
  declared `STATE_FREE` / `STATE_RENTED` privately. After writing a section here, grep the new
  code for what the section forbids.
- **A name in a comment is checked like a name in code.** `VariableFxConfigSnapshot` sent
  readers to a `VariableFxUpdateFlags` that was never written. No gate resolves a `<c>` tag:
  use `<see cref>` for a type that exists, and grep for one you only mention.
- **A summary says what the method does now.** `GetRoomSettingsMessageHandler` began to send a
  settings error before `NoSuchFlat` and its summary still described the old reply. Read the
  doc comment of whatever you change the behaviour of.
- **Remove what a change orphans**: the setting nothing reads, the interface member with no
  caller, the using, the appsettings key. A setting added "for later" (`OnlineTimeMinutes`,
  `MinutesBetweenMountAttempts`) is an orphan from the day it lands: add it with the code that
  reads it. An option that moves to another config class takes its key along:
  `TrophyInscriptionMaxLength` moved from `RoomConfig` to `CatalogConfig` and its key stayed under
  `Turbo:Rooms`, where the binder ignored it. `ShippedAppSettingsTests` now fails on a key in a
  shipped settings file that no option reads.
- **A `new` member that hides a base one is drift, not a specialisation.** `RoomAvatar` declared
  `public new void SetRotation` that turned the body *and* the head, over `RoomObject`'s
  body-only `SetRotation`. Which one ran depended on the static type of the variable: the
  concrete avatar classes got both, and every caller holding an `IRoomAvatar` — the walk step,
  room entry, riding, the bot and pet modules — turned the body and left the head behind. Make
  the base member `virtual` and `override` it, so the call means the same thing everywhere, or
  give the specialised one its own name. A behavioural difference that depends on the reference
  type is invisible in review and no gate catches it.
- **A second name for an inherited method is a copy of it.** `RoomAvatar.SetHeight` was
  `RoomObject.SetPositionZ` written out again, word for word, and `IRoomAvatar` published both.
  Before adding a method to a subclass, look for what the base already calls that job.
- **State the `Users` packet does not carry is replayed on room entry.** A dance, an effect
  and a sleep are each their own update, so an arriving player is sent one per avatar that has
  one, beside the `Users`/`UserUpdate` pair in `RoomService`. `IsIdle` was on `IRoomAvatar` but
  not on `RoomAvatarSnapshot` at all, so everyone who had dozed off before you walked in was
  drawn awake until they next moved. A snapshot field that exists only to be replayed says so
  on itself, as `DanceType` and `EffectId` do.
- **A composer field is named and typed for the id it carries.** `SleepMessageComposer.UserId`
  was an `int` that all three call sites filled with `avatar.ObjectId`. The room object id and
  the player id are different numbers and the name claimed the wrong one; its siblings in the
  same folder (`DanceMessageComposer`, `AvatarEffectMessageComposer`) take a `RoomObjectId`,
  and that type is what keeps the two from being swapped.
- **A `Try...` method annotates its out parameter.** `[NotNullWhen(true)] out TItem? item` puts
  the guard in the signature, so a caller that checks the result is done. Without it the check
  becomes a convention: three of the four furniture variables wrote `|| item is null` after
  `TryGetItemForKey` and the fourth forgot, which the compiler reported as two nullable warnings
  nobody read.
- **An exception is not control flow, not even inside the method that catches it.**
  `ValidateAvatarStepAsync` threw `TurboException` to mean "this step is blocked" and caught it
  a few lines down to stop the walk. A blocked step is the ordinary end of a walk, so it is a
  guard clause and a `return`; and because the same `catch` swallowed everything else without
  logging, a real failure in the walk path looked exactly like a taken tile.
- **A stub handler has one shape.** Two hundred and seventy-one handlers answer a packet the
  server has no system for with `await ValueTask.CompletedTask.ConfigureAwait(false)` and a
  summary saying what is missing. A handler that instead keeps its body commented out, and the
  injections that body needed, is neither working code nor an honest stub:
  `GetMessengerHistoryMessageHandler` once carried the `IConfiguration` `AGENTS.md` says handlers
  no longer take, for a grain method that was never written.
- **A buffer nothing reads is half a feature, and it does not announce itself.** The messenger
  grain once filled a per-conversation history, capped it with `MaxSessionMessagesPerConversation`,
  and no caller ever read it, while `ConsoleMessageHistoryMessageComposer`, its serializer and its
  header sat in place with nothing to send them. Neither end looked wrong on its own. (History is
  now paged from the message rows, and the buffer is gone.) When adding state, add the reader in
  the same change — or when finding one, say which of the two ends is missing rather than
  deleting the one you happened to open.
- **A change the client applies itself is sent as that change, not also as a fresh list.** An
  ignore at the limit sent the whole ignore list and then `IgnoreResult` "oldest removed"; the
  client took the list and then dropped its first entry again, so two players left its copy for
  one. Read what the client's handler does with each packet before sending both a snapshot and a
  delta, and keep the server's copy in the order the client assumes (oldest first there).
- **Zero warnings is the baseline, and an incremental build hides them.** A clean tree builds
  `Turbo.Tests`, and with it `Turbo.Main`, with no warnings at all, so any warning belongs to the
  change in front of you. Build the tests, not only the host: `Turbo.Main` does not compile them,
  and the test project carried four analyzer warnings (`VSTHRD003`, `VSTHRD103`, two `xUnit1051`)
  that no host build showed. Grep the log for `warning [A-Za-z]+[0-9]+`; xUnit's ids start in
  lower case.
  MSBuild only reports warnings for projects it actually recompiles, so a project that was
  already up to date stays silent about its own: `RoomSnapshot`'s uninitialised `ModSettings`
  went unreported for as long as `Turbo.Primitives` was not rebuilt. Build to a scratch
  `-p:OutDir` (which also keeps clear of a running debugger) to see the whole tree's output.
- **A reference sample nothing compiles is a rule that rots silently.** `docs/patterns/*.cs` is
  what the load order tells every tool to start from, and no project builds it:
  `TurboCloudAiGovernanceCheck` asserts only that the three files exist. All three had drifted —
  `ServicePattern` returned a `PlayerSummary` that is `PlayerSummarySnapshot` and has been for
  some time, `HandlerPattern` showed a sealed handler with `ct.ThrowIfCancellationRequested()`
  and a null check on the parsed message, which none of the 502 real handlers does, and
  `UnitTestPattern` used `xunit` and `FluentAssertions`, neither of which is in
  `Directory.Packages.props` (there was no test project then; there is now, `Turbo.Tests`). Each sample now names the real
  file it mirrors; when a convention changes, change the sample in the same commit, and check
  its type names by hand because nothing else will.
- **An option is read through its config class, never by key name.**
  `pluginSection.GetValue<bool>("HotReloadEnabled")` was the only place in the repository doing
  otherwise. A string key survives a rename of the property it names, and it answers `false`
  for an absent section instead of the option's own default — so `HotReloadEnabled = true` on
  `PluginConfig` was dead. Bind the section (`section.Get<TConfig>()`) or take
  `IOptions<TConfig>`, and read the property.
- **A packet handler takes a primary constructor.** Five hundred of the five hundred and two
  already did; the two extended-profile handlers were still writing out a field, a constructor
  and an assignment. (Grains are the opposite, see [Grain Rules](grains.md#every-grain-has-the-same-shape): a grain takes a classic
  constructor, because it has a body to run.)

- **A grain pair with a call in both directions deadlocks; decide the direction and write it
  down.** Orleans grains are not reentrant, so if A is inside a call to B while B calls A, both
  wait for ever. The group system has three such pairs and every one of them was written the
  wrong way round first:
  - the room grain asks the **group** grain for a member's rank on every controller-level check,
    so the group grain may never *await* a room grain;
  - the group grain asks the **player's** guild grain for their membership count before a join,
    so that grain may never await a group grain;
  - the player's guild grain tells the **room** its owner's badge changed, so the room may not
    read that badge back out of the player's grain — it arrives as arguments.

  A push in the barred direction is still the owner's to make: it goes out from the grain that
  changed, fire-and-forget. `GuildGrain.Notify` tells rooms and the player's guild grain with
  `LogAndForget` (see "Moving the call to the caller is the wrong half of the fix" in [Grain Rules](grains.md#grains-are-not-reentrant-side-effects-on-other-grains-that-call-back-go-in-the-service));
  `GuildGrain.DeactivateAsync` tells the homeroom `OnGuildDeletedAsync` the same way. An
  earlier version put these pushes in handlers (`GuildFurniRefreshExtensions` and friends);
  they are gone, and handlers are guard, call, report.

  Before adding an awaited call between two grains, ask what the callee already awaits. If the
  answer leads back to the caller, make it a tell — `LogAndForget`, or an interleaved
  memory-only method on the callee.
- **A cache that answers "no" must be invalidated when the answer becomes "yes".** `RoomGrain`
  holds whether it is a group's homeroom, and a room that is nobody's homeroom caches that so it
  does not re-ask on every rights check. Creating a group whose homeroom was already loaded —
  the usual case, since the wizard is opened from inside it — left the room certain it was an
  ordinary room, so the owner had none of the rights their own new group had just given them.
  A negative answer needs the same invalidation path as a positive one.
- **A change to who may do something has to reach whoever is standing in the room.** Joining,
  leaving, being approved, promoted or demoted changes a player's rights in the group's
  homeroom. Nothing re-derives that on its own: the group grain's
  `NotifyHomeroomMemberChanged` tells the homeroom `RefreshGuildMemberAsync`, which refreshes
  that one player if they are in the room.
  Without it a player keeps whatever they walked in with until they leave and come back.
- **A fixed-width wire format needs a guard where the value enters, not a comment.**
  `GuildBadgeCodes` writes a badge as six-character tokens: prefix, two digits of part, two of
  colour, one of position. `BASE_KEY_MAX`, `SYMBOL_KEY_MAX` and `COLOR_KEY_MAX` described that
  and were enforced nowhere, so a hotel that seeded a part id past 99 (or a symbol past 199)
  would have produced codes that read back as a different badge entirely. The limits are now
  checked in `GuildBadgeParts.Sanitize`, where parts arrive. Constants that state a limit but
  guard nothing are the same dead weight as an unused config key.
- **`dotnet format ... --diagnostics IDE0005` reports nothing; unused usings need a real build.**
  IDE0005 does not fire without a documentation file, and the repository sets no severity for
  it, so neither gate sees an unused using. To sweep for them without touching `.editorconfig`:
  write a `.globalconfig` (`is_global = true`, `dotnet_diagnostic.IDE0005.severity = warning`)
  and a targets file adding it as an `EditorConfigFiles` item, then build `Turbo.Tests` (it
  compiles every project, and most dead usings were in the tests) with
  `-p:CustomAfterMicrosoftCommonTargets=<targets> -p:EnforceCodeStyleInBuild=true
  -p:GenerateDocumentationFile=true --no-incremental` and grep for IDE0005. (Adding it as a
  `GlobalAnalyzerConfigFiles` item through `CustomBefore…` is not picked up.) Verify the sweep works by adding a deliberately unused `using` first — a
  silent run means the detector is off, not that the tree is clean. Text heuristics do not
  substitute: they misfire on attributes, enum types and extension methods.
- **Dead state is a lie about what the grain knows.** `GuildLiveState.IsLoaded` and its twin on
  the player's grain were set on every load and read nowhere, which reads as though something
  distinguishes "not loaded yet" from "no such group" when nothing does. Either read the flag or
  delete it.
- **An entity-to-snapshot extension that nothing calls is worse than none.** `GuildEntityExtensions`
  grew a `ToSnapshot` for `GuildMemberEntity` while both real call sites built the snapshot from
  a column-trimmed query projection, which [Types & Constants](types-and-constants.md#type-placement) sanctions. The extension was dead and would
  have been adopted by the next person as the "right" way, quietly adding a second mapping. If
  the projection is correct, delete the extension.

- **Lazy resolution and a snapshot handed out by reference do not mix.** `RoomGrain` resolved
  which group owns it on first ask and stamped the answer onto `_state.RoomSnapshot`. But
  `GetSnapshotAsync` returns that snapshot directly and awaits nothing, so whoever asked for the
  room's listing first got one with no group on it — the room had never been given a reason to
  look. Anything a snapshot carries that is not on the row it was built from has to be resolved
  **while the room loads**, not when something first happens to ask.
- **Clearing a cached lookup means clearing everywhere it was copied to.** The same resolution is
  held twice: on a field, and stamped onto the room snapshot. Invalidating only the field left a
  failed re-read advertising a group the room could no longer confirm — no rights, but still the
  badge. If a value is cached in two places, the invalidation path has to name both.
- **Two `SaveChangesAsync` calls are two chances to half-finish.** Creating a group wrote the
  group, saved, then wrote its owner's membership and saved again. A failure between the two
  left a group nobody was in, whose owner then stood in their own homeroom with no rank and so
  no rights. Add both and save once: EF orders a principal and its dependent itself and fills
  the foreign key, as long as the dependent points at the **entity** rather than at its id. The
  same rule [Grain Rules](grains.md#use-tracked-deletes-for-atomicity) states for delete-plus-insert applies to insert-plus-insert.
- **Money moves before the thing it buys exists; say what happens when the thing fails.**
  Creating a group charged the buyer and then wrote the rows, with nothing around the write. A
  database error took the credits and gave nothing back, and the player has no way to see it
  happened. Where a debit cannot share a transaction with what it pays for — it never can, the
  wallet is another grain — the write goes in a `try` and the failure path refunds. A refund that
  itself fails is logged and swallowed: there is no third place to put the money, and throwing
  would lose the original error.

- **Every field of a cached copy needs an invalidation path, not just the one you were thinking
  about.** A room holds its group's summary — id, **name**, **badge**, colours, type — and draws
  its navigator listing from it. Changing the group's settings refreshed it; renaming the group
  and redrawing its badge did not, because those two had been written while thinking about the
  furni they repaint, and the furni reads the directory rather than this copy. A rename left the
  room advertising the old name indefinitely. When a cache holds a record rather than a single
  value, enumerate what the record contains and check every writer of each field, not the field
  that prompted the change.
- **The unused-using sweep, and why it has to be a sweep.** Nothing in either gate sees an unused
  `using`: IDE0005 needs a documentation file and the repository sets no severity for it, so
  `csharpier`, `dotnet format` and the build are all silent. The recipe is above; run it over the
  whole tree rather than the files you touched, because removing one `using` can make another
  unnecessary, and the build reports only the first `using` of an unbroken run of dead ones, so
  a long run sheds one a round. A full pass took four rounds once (107, 7, 2, none) and eleven
  the next (516, 75, 29, 14, 6, 2, 2, 1, 1, 1, none), the tail being one test file that had
  twenty-five. Re-run `csharpier format` between rounds and confirm the diff contains nothing
  but `using` lines before trusting it. Leave `docs/examples` out: the room window example's
  sources compile into `Turbo.Tests`, whose `GlobalUsings.cs` makes their `System` usings look
  dead, and the plugin compiler that builds the example as a hotel does needs them.

- **A handler that does anything after the grain call is a design smell.** A packet handler
  guards its inputs, calls one grain method, and turns the answer into a packet. If it is also
  refreshing a room, repainting furni or invalidating a cache, that work belongs to whatever owns
  the state — see the reentrancy section for how to push it without deadlocking. Nine guild
  handlers had grown a refresh block each; all nine are now guard, call, report.
- **Identical failure reporting in N handlers becomes wrong in one of them.** Seven roster
  handlers each spelled out which composer carries which kind of refusal, and three had grown a
  branch for a failure their own operation cannot produce. One extension
  (`GuildMemberMgmtResultExtensions`) now owns that mapping and returns whether the operation
  went through, so the one caller with more to do can stop. Repeated packet-shaping across
  handlers of the same family is worth an extension in the same folder, the way
  `LegacyRoomSearchExtensions` already is, or a domain service when the handlers need more than
  shaping (the friend list's `IMessengerService`, the profile's `IPlayerService`). See [Packet
  Handler Rules](handlers.md) for the shape.
- **The copy nobody updated is the one that is wrong.** A sweep for duplicated handler logic
  found drift in almost every family that had more than one copy:
  - The room-ad purchase had its own catalog error mapping. It sent `RequiresHabboClub` as the
    raw code 101, so the client showed "unknown" where it should have said club required.
  - The LTD raffle sent "not enough credits" on the refusal packet. That packet words only code 1.
  - `MoveWallItem` parsed wall positions by hand. It did not use `WallPosition.TryParse`, so
    short input threw and `z` was parsed with the server's locale.
  - `RequestABadge` left the client with no answer for an unknown request code. Its sibling
    answered "not fulfilled".

  Each family now goes through one extension or parser. Before writing a handler, look for a
  sibling that already does the same thing and call what it calls.
- **A bidirectional await can be introduced by an innocent-looking read.** `RoomAvatarModule`
  awaits the player's guild grain when somebody walks in, to learn the badge they wear. That made
  the favourite-group setter — which awaited the room, to tell it the badge changed — a deadlock
  waiting for the two to happen together. Nothing about either call site looked wrong on its own.
  When adding a read from grain A to grain B, grep what B already calls on A before deciding the
  direction is free.

- **A predicate over an enum belongs beside the enum, not at each call site.** "Is this player in
  the group" was written nine times in four spellings: `rank is (Owner or Admin or Member)`,
  `CountOfRanks(Owner, Admin, Member)`, an `[Owner, Admin, Member]` array for a query, and —
  twice — the *inverse*, `Rank != Requested && Rank != Blocked`. All nine read correctly, and the
  two written by exclusion would have disagreed with the rest the moment a rank was added,
  silently counting it as a membership. `GuildMemberRanks.IsMember` / `CanManage` are now the only
  definitions, with `MemberRanks()` / `ManagingRanks()` returning fresh arrays for queries (a
  plain array is what EF turns into an `IN`, and a fresh one cannot be edited by a caller).
  When the same question about an enum is asked in more than one file, give it a name next to
  the enum — and prefer stating it by inclusion, so a new member defaults to "no".
- **A helper duplicated across modules belongs in `Turbo.Primitives`.** Clamping client text to a
  stored length existed four times: the navigator, room settings and both guild grains. The two
  guild copies had left the `Trim()` out, so a group could be named with leading spaces where a
  room could not — the drift was already there, in behaviour, not just in line count. It is now
  `Turbo.Primitives.Texts.ClientText.Truncate`. Before writing a small string, id or number
  helper, grep for the body, not the name: these four were spelled `Truncate`, `Truncate` and
  `Clamp` twice.
- **A mapping that takes resolved values invites the resolution to be repeated.**
  `ToSummarySnapshot(entity, hasForum, primaryColor, secondaryColor)` made each of its three
  callers write out the same two palette lookups. Taking the palette itself
  (`ToSummarySnapshot(entity, palette, hasForum)`) keeps the mapping a pure function — a snapshot
  is data, not a provider, so the [Type placement](types-and-constants.md#type-placement) rule still holds — and leaves one copy of the lookup.
  Pass the **source** a mapping needs, not the answers, unless the caller genuinely knows
  something the mapping cannot.

- **"Which subscription counts as club" is one decision, so it has one call.** Two grains each
  wrote `GetPlayerSubscriptionGrain(x).HasActiveAsync(SubscriptionType.HabboClub, ct)`, with
  different signatures for the same question — one taking a player id, one using its own. It is
  now `IGrainFactory.HasActiveClubAsync(playerId, ct)`, beside `SendComposerToPlayerAsync` in
  `GrainFactoryExtensions`, which is where a one-line grain call that encodes a rule belongs.
- **An awaited call that is safe "because of the order things happen in" is worth not awaiting.**
  `GuildGrain.DeactivateAsync` awaited the room to hand back the homeroom's furni, and it was
  genuinely safe: the group is out of the directory by then, so the room resolves no group and
  cannot ask the group grain anything. But that safety is a property of the room's furni-removal
  path, which is somebody else's to change. It is `LogAndForget` now, and the pair cannot deadlock
  whatever either side does later. Prefer removing the edge to documenting why the edge is fine.

- **An offer's price is read in one place.** `CatalogOfferSnapshot.ToDebitRequests(quantity)` is
  what buying it takes out of a wallet. The LTD raffle had its own copy that left out silver, so
  a silver-priced LTD was handed out for free. Do not build `WalletDebitRequest`s from an offer
  by hand; `CurrencyKind.Credits` / `Silver` / `Emeralds` / `ActivityPoints(type)` name the kinds.
- **Who may buy an offer is checked on every path that sells it.** `CatalogOfferSnapshot.
  RequiresClub` (club level above none; this client collapses the tiers, so any active
  membership meets it) is checked by the shop purchase, the room ad and the LTD entry. The level
  was sent to the client and checked nowhere, so a club-only offer could be bought by anyone who
  sent the packet.
- **Every "charge, then create" path refunds through `IGrainFactory.RefundAsync`.** Group
  creation refunded; the catalog grant, the room ad and the LTD draw did not, so a failed write
  after a successful debit kept the money. The debit can never share a transaction with what it
  pays for, so: debit, then `try` the creation, and on failure (thrown *or* refused) refund and
  rethrow. Only wrap what the money bought — a failure after the thing exists (a notification, a
  subscription extension that tells the player) must not refund a purchase that landed.
- **Every path to a state checks the same limit.** Joining a group checked the member cap;
  approving one request and approving the lot did not, so an exclusive group could be approved
  past it. The limit is now a question with a name (`GuildGrain.FreeMemberSlots`) that all three
  ask. When you find a limit checked in one path, list every other way to reach that state.
  Two copies of one limit are two paths waiting to disagree: creating a room kept to
  `Turbo:Navigator`'s name, description, visitor and tag limits and every later change to
  `Turbo:Rooms`', so a hotel that raised one had rooms it could make but not edit. A limit
  belongs to what it limits (`RoomConfig`), and every path reads it there. A limit the client
  keeps in its own text field is no limit at all: a player could set a motto of any length,
  while a new account was held to 38 (`PlayerConfig.MottoMaxLength` now holds both).
- **A tool outside the server asks the server's rules, it does not copy them.** The
  Morningstar import decided an item's logic with its own copy of the room's classname rules,
  which had one of their eight swaps (wired boxes). Turbo's `bb_counter` is plain furniture by its
  logic column and a game timer in a room, so the import wrote its time for plain furniture and
  every counter came across at 0. It also wrote a wired snapshot's height rounded where the room
  truncates (`Altitude.ToInt`), so a furni at 0.29 never matched its own snapshot. The room's
  rules are now `RoomObjectLogics` (which logic exists, which one a definition runs) and the
  import is handed them (`ImportStartup.Options`); a value the room compares is computed the way
  the room computes it.
- **A count the client sends is read with `PopList` / `PopCount`, never `PopInt`.**
  `PopList(bytesPerItem, read)` bounds the count by what the packet can hold and reads the
  entries; twelve parsers had the loop by hand, and two used a raw `PopInt` — one of them sized a
  dictionary from it, so one packet could ask the server to allocate a billion entries. A later
  sweep found four more loops behind six parsers (the crafting mixer, poll answers, marketplace
  offers, call-for-help chat lines; `ClientCountParserTests` covers the first three). The one
  raw count left is `VariableLeaderboardReplaceSubscriptionMessageParser`'s, which needs it to
  refuse a list longer than Flash sends.
- **A row two packets carry is written by one serializer.** The inventory and the trade window
  both start an item with id, type and *ref*, and the client locks an inventory item while it is
  in a trade by matching that ref. Inventory wrote a floor item's ref negated, trade wrote it
  positive, so no floor item was ever locked. `FurnitureItemSerializer.WriteHead`,
  `BadgeEntrySerializer` and `OwnerNamesSerializer` exist for that reason; when a second
  serializer needs a structure, move it to `Data/` before writing it again.
- **Text the client prints as-is has one format.** `ClientDates.Format` (dd-MM-yyyy) for every
  date the client shows, `HexColor.IsRgb` for colours, `ClientText.Truncate` for lengths. Four
  copies of the date format existed and the profile used a fifth; two colour checks disagreed on
  whitespace.
- **A reload replaces, it does not clear and refill.** A provider that clears its maps and then
  loads is empty after a failed reload; build the new maps and swap them in
  (`FurnitureDefinitionProvider`, `CurrencyTypeProvider`).
- **A special case for one item behaves like the general case.** `GetPlayerNamesAsync` answered
  `""` for an unknown id when asked about one, and left it out when asked about several. If a
  fast path is worth having, it returns what the slow path would.
- **Stop releases everything start acquired.** `NetworkManager` started a TCP and a WebSocket
  host and stopped only the TCP one. Review a start and its stop side by side.
- **A value computed and then not used is a bug report, not dead code.** The plugin table-prefix
  code computed a prefix and returned the raw one, so a plugin with none got `""` — and
  uninstalling it ran `DROP TABLE ... LIKE '%'` over the whole database. `MigrationHelper` now
  refuses an empty or non-identifier prefix. Before deleting an unused value, find what it was
  meant to protect.

- **A later optional wire field forces the earlier one to be written.** The client's chat parser
  reads a receiver index if bytes remain, then a bubble width if bytes remain; the serializer
  wrote the width only when there was a receiver, so bot bubble widths never arrived. Write the
  client's "unset" value (-1) for the earlier field instead of skipping it.
- **A separator in a wired string param is whatever the client's `readStringParamFromForm`
  joins with.** The name selector split on `/`; the client sends tabs, so a list of names matched
  nobody. Read the editor class before parsing its string.
- **Who may use an item is `IFurnitureLogic.CanUseAsync`, not the usage policy alone.** The
  policy is also what the client draws a use button for, so an item whose use is one person's
  (a monster plant seed, a pet package: the client offers them to the owner and sends a plain
  use) keeps its policy at Nobody and overrides `CanUseAsync` to `IsItemOwner`. Both used to
  have real `OnUseAsync` bodies behind a Nobody policy that refused every use, so planting a
  seed and opening a pet package did nothing. A usage-Nobody logic that does not override
  `CanUseAsync` never has `OnUseAsync` called — do not give it one.

- **A wired box moves a floor furni with `IWiredExecutionContext.TryMoveFloorItemAsync`.** It
  asks `CanPlaceFloorItem` and then moves, in one call. Ten movers each wrote the check and the
  move out by hand, "furni to furni" and "move furni to" were the same loop (now
  `FurnitureWiredActionLogic.MoveOntoTargetFurniAsync`), and the copies had drifted: chase pinned
  the furni's height where its mirror, flee, let the room's move physics decide, and the two
  twins offered different sources for their target slot. Height is the policy's (the
  `KeepAltitude` addon), not the box's; pass a `z` only when the box itself sets one (match to
  snapshot, placement variable).
- **A room writes a furni's placement through `WritePlacement`.** `FurnitureEntity` and
  `BuildersClubFurnitureEntity` share their placement columns through `IPlacedFurnitureEntity`,
  and the persistence grain marks exactly those modified, by name, for both. Two hand-written
  copies of that block had already grown apart (one marked the room column three times).

- **The per-packet path is shared by every player, so it is held to a stricter budget.**
  - Nothing on it awaits a grain. Decoding, `MessageRegistry.CreateContextAsync` and encoding
    used to cost a presence-grain round trip per incoming packet just to read the active room;
    the room now lives on the session (`ISessionContext.ActiveRoomId`), pushed by the presence
    grain. Any change to the presence's `_state.ActiveRoomId` calls `OnActiveRoomChanged()`.
  - A broadcast is serialized once, not once per recipient: `ComposerPayloadCache` keeps the
    framed, unencrypted bytes per composer instance and each session encrypts its own copy. So
    **a composer, and any collection it holds, is never changed after it is handed to a send**,
    and **a serializer is a pure function of its composer** — no clock, no randomness, no
    session state. Compute "seconds ago" where the composer is built.
  - A `ServerPacket` writes into a pooled buffer: copy its bytes out, dispose it, never keep its
    `WrittenSpan`.
  - The presence drains its whole queue into one observer call, and a room tick that emits
    several composers sends them as one `SendComposersToRoomAsync` / `SendComposersToRoomAndForget`:
    one stream item is one flush per recipient.
  - A debug log on this path is guarded with `IsEnabled`; `LogDebug(template, args)` allocates
    its argument array before it checks the level.
  - The byte-for-byte harness that proved the pooled writer, the RC4 span path and the cache
    changed nothing on the wire lives outside the repo; rebuild one before changing
    `ServerPacket`, `PackageEncoder` or the RC4 engine.
- **`LogAndForget` names its ids as template arguments.**
  `.LogAndForget(_logger, "set player {PlayerId} online", playerId)` makes the id a searchable
  log property and formats nothing unless the task fails; `$"..."` built the string on every
  call, success or not, about seventy-five times on hot paths.

- **A hotel-wide singleton never makes its readers wait for its own refresh.** The badge
  directory, guild directory, Builders Club and leaderboard refresh on a timer registered with
  `GrainTimerCreationOptions.Interleave = true`: the query reads into locals and the result is
  swapped into state with no await in between. A tell that lands during the read is recorded
  and replayed after the swap (`GuildDirectoryGrain`'s changes-during-reload). Before, every
  badge display, room activation and Builders Club placement queued behind a hotel-wide
  `GROUP BY`. The same goes for a directory's cache miss: `PlayerDirectoryGrain`'s reads are
  interleaved and only fill the cache for a player it does not already hold.
- **A fan-out tells only the recipients that can act on it now.** A player's profile change
  went to every friend's messenger, offline ones included, and each woke up, loaded five
  queries and asked all of *its* friends' presences whether they were online — one login cost
  hundreds of queries and thousands of activations. Online friends are told; offline ones read
  fresh rows when they next load. Never wake a grain so that it can drop a message.
- **An activation most callers make for a cheap read loads rows and nothing else.** The
  messenger is woken by profile views, the raffle's weighting and friend requests; it resolves
  who is online, starts its timer and fans out only on its owner's first real use
  (`EnsureFriendsOnlineResolvedAsync`). Say which calls pay for what in the grain's summary.
- **A lazy step that awaits must not overwrite what interleaved tells changed meanwhile.** The
  online resolution replaces a friend entry only if it is the same object it read before the
  first await; a `RecieveFriendUpdateAsync` that ran during the await wins.
- **A migration that swaps a single-column foreign-key index for a composite creates the new
  one before dropping the old.** EF emits the drop first and MySQL refuses to drop an index a
  foreign key still needs; reorder `Up` and `Down` by hand and read the generated SQL.

- **The room tick never waits on a database write.** The room awaited the persistence grain
  on every 50 ms tick and every chat line, and that grain's timer was mid-`SaveChanges` often
  enough to stall the room for the length of a write. Now:
  - a buffer grain's `Enqueue*` methods are `[AlwaysInterleave]` and memory-only, and stay
    awaited, because the order of hand-overs matters (a pick-up then a re-place);
  - the room hands things over once per `DirtyItemsTickMs` (`RoomGrain.Persistence`), not per
    tick — pets and bots are *marked* dirty and snapshotted at hand-over, so one changed ten
    times is written once and one that left the room is skipped; chat lines are buffered.
- **A room with no players and no NPC still walking is dormant.** It skips NPC, avatar, roller,
  variable-fx and periodic-wired work and slows its tick to `AvatarTickMs`, but keeps
  persistence, scheduled actions and item timers; a player entering wakes it
  (`WakeTick`). Anything that must happen in an empty room goes in the kept list — or, like pet
  decay, runs on wall-clock time and catches up: a pet's next energy and nutrition decay are
  persisted as UTC due times (`EnergyDecayDueUtc` / `NutritionDecayDueUtc`), and its first tick
  after the room loads or wakes takes every period that fell due meanwhile. Game time that
  should pass while a room is away is never kept on the room's own clock (`NowMs`), which
  starts again with each activation.
- **Room code reads time from the room's clock, never the machine's.** `NowMs()` (monotonic)
  and `UtcNow` on `RoomGrainComponent` both read the grain's injected `TimeProvider`; a
  `Stopwatch` or `DateTime.UtcNow` read in grain-run code is a time a test cannot move. The
  room harnesses run on a `ManualTimeProvider` (`.Clock`) that stands still until the test
  advances it, so a room test steps time exactly instead of waiting for it. A wall-clock
  measurement of work done (how long a command or a wired pass took) stays on `Stopwatch`.
- **NPCs decide on the avatar tick, and a failed follow backs off.** A pet following an
  unreachable player ran up to eight full path searches, each ending in an exception, every
  50 ms. Pets and bots decide at the 500 ms boundary (walking only advances then);
  `RoomAvatarModule.IsFollowDue` re-searches only when the target moved or after
  `NpcFollowRetryMs`. `WalkAvatarToAsync` refuses with `false`, never an exception.
- **Group-derived controller levels are cached in the room** and cleared from the hooks that
  already refresh rights (`RefreshGuildMemberAsync`, `OnGuildChangedAsync`) and when the
  player leaves. Every furni use in a homeroom used to await the group grain.
- **A cache of "which items are still in the room" keys on `RoomLiveState.ItemsVersion`**,
  which moves on every attach and detach (the wired boxes' validated stuff ids use it).
- **A cheap trigger pre-match may only reject what `CanTriggerAsync` would reject**, and stays
  out of the way when the trigger's selection comes from the selector pool, which is not filled
  until the selectors have run.
- **Composers are batched per wired action, not per tick.** Other routes (chat, furni added or
  removed) send mid-tick, and a per-tick batch would reorder against them. The roller, which
  has no such interleaving, batches its whole tick.
- **Walking paths are stored goal-first**, so the next step comes off the end.
- **An entry-time read of room state is one call** (`IRoomGrain.GetEntryViewAsync`); entry used
  to make nine sequential calls to the same room, each queued behind its tick.

- **A copy the reader already holds carries what the reader needs.** A group homeroom works out a
  member's rights on every build and pick-up. It held the group's summary, but the decoration
  setting lived only on the full snapshot, so a second method fetched the whole group from the
  group grain for that one field and kept a second cache for it, with its own invalidation. The
  setting is on `GuildSummarySnapshot` now and the level is one method: one rank call per player,
  the rest from the summary. Before adding a grain call for one field, check whether the copy in
  hand can carry it; the copy's existing refresh path (here `OnGuildChangedAsync`) then keeps it
  current for free.
