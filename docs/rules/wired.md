# Wired Rules

- [Wired chests](#wired-chests)
- [Wired](#wired)
- [Variable fx](#variable-fx)
- [System boundaries: wired is a user of the room, not a part of it](#system-boundaries-wired-is-a-user-of-the-room-not-a-part-of-it)

Rules for wired boxes, wired chests, variable fx, and the boundary between wired and the rest of
the room. A plugin's box is hosted by core's own kinds, so these hold for it too
([Wired](../plugin-wired.md) is the plugin author's page).

## Wired chests
- A chest's contents are `IWiredChestGrain`'s (keyed by the chest's item id), not the room's.
  The furni it holds are ordinary `furniture` rows with `chest_item_id` set, owned by the
  chest's owner and in no room, so they keep their id, serial and extra data. Every query that
  lists an inventory excludes them (`ChestItemEntityId == null`); a new one must too, or a
  stored furni shows up in its owner's inventory as well as in the chest.
- Every move commits the rows, the credits (`wired_chests`) and its log entry
  (`wired_chest_transactions`) in one database transaction. A deposit takes rows the depositor
  holds in no room and no chest and then tells their inventory to let go
  (`ReleaseFurnitureAsync`, memory only, like a placement); a withdrawal re-owns rows and tells
  the receiver's inventory (`ReceiveFurnitureAsync`). Credits leave through the wallet's
  referenced credit (`wiredchest:<transaction id>`), and a refused credit puts them back.
- The calls go one way: the room awaits the chest grain; the chest grain awaits inventories
  and wallets, never a room. The player's trade window is `IWiredTradeGrain` (keyed by player);
  the room only tells it, and it awaits the room, which carries the deposit out
  (`DepositIntoWiredChestAsync`).
- The settings (lock, capacity, name, appearance, notifications) are the chest furni's map
  stuff data under the client's keys (`WiredChestData`), and the logic
  (`FurnitureWiredChestLogic`) decides who may do what. It mirrors the grain's counts into
  `Summary`, which conditions read without awaiting. Capacities and upgrade prices are
  `WiredChestConfig`, shipped as the client's own config defaults; change both together.
- Credits given by the give-currency box go to the wallet. The client files them under an
  earnings category to claim later; this server has no earnings to claim.
- A contract (`wired_contract_payment` / `_trade` / `_reward`) is its furni's extra data
  (`WiredContractData`), checked against the editor's limits when saved. Initiate Transaction
  pays a reward out at once (`RoomWiredTransactionSystem.RewardAsync`); a payment or trade
  opens the player's `IWiredTradeGrain`, which holds the offer to the contract
  (`WiredContractOffers`, the client's own counting) and, on confirm, has the room take the
  payment into the chests and pay the reward out of them
  (`IRoomGrain.CompleteWiredContractTradeAsync`). The room checks the chests can cover the
  reward before it takes the payment. Every end, good or bad, is a
  `WiredTransactionCompletedEvent` / `WiredTransactionFailedEvent` for the triggers.
- Cancel Transaction only marks the trade; it ends after `WiredChestConfig.CancelGraceMs`, so
  an Initiate Transaction on the same stack replaces it without the window closing, which is
  what the box's usage text promises. A second Initiate while a trade is open is refused as
  "already trading".
- A custom contract addon puts itself on the policy (`IWiredPolicy.CustomContract`); the
  Initiate box builds the contract from it at run time, and the transaction is then the box's,
  which is what the triggers select.

## Wired
- Every wired box is a `[RoomObjectLogic("wf_...")]` under `Turbo.Rooms/Object/Logic/Furniture/Floor/Wired/`
  deriving from the base of its kind (`FurnitureWiredTriggerLogic`, `...ConditionLogic`,
  `...SelectorLogic`, `...AddonLogic`, `...ActionLogic`, `...VariableLogic`). The boxes on one
  tile form a stack that `RoomWiredSystem` runs: trigger → selectors (filter and invert are
  applied there) → addons mutate the `WiredPolicy` → conditions → the actions the outcome picks.
  Actions whose `IsNegative` is set (the `wf_act_neg_*` boxes) run when the conditions fail.
- A plugin's box is not one of these: it derives from a contract (`Turbo.Contracts.Wired`) and is
  hosted by `Object/Logic/Plugins/PluginWired*Logic`, core's own kinds that read its declaration
  (`FurnitureWiredLogic.Plugins.cs`), so every rule here holds for it too. A rule added to a base
  class reaches plugin boxes by itself; one added per box needs a contract member
  (`docs/plugin-wired.md`).
- Int params follow the Flash client editor exactly; declare them with `GetIntParamRules()` and
  read them with `GetIntParamOrDefault`, as the type the rule declares: a
  `WiredEnumParamRule<T>` param is read as `T` (fallback `T.Member`), a bool rule as `bool`.
  Reading an enum param as `int` and casting throws inside the accessor, and the fallback
  hides it: `GetTargetType` did that, so every box with a variable target ignored the target
  it was saved with and logged an exception on each run. A mismatch is now logged as an error
  naming the box class; treat that line as a bug in the box, not as bad data. A variable
  target is read through `GetTargetType(variable, index)`, not by hand.
  All three cases found were in shared helpers that take the index as an argument, where the
  rule is out of sight: `GetTargetType`, `TryResolveOperand` (its value-or-variable switch
  always read as "value", so an operand variable never worked) and `RequiresAll` (the "all
  must match" switch always read as off). A helper that reads a param says in its summary
  which rule the box must declare there, and a new one is checked against every caller's
  `GetIntParamRules()`. Two-int longs (`pushIntAsLong`) are read with
  `GetLongParam`, the "value or another variable" operand block with `TryResolveOperand`.
  Two-slot boxes (move furni to, furni to furni, send signal) read each slot with
  `WiredSlotSelection.ForSlot`; the second slot is `StuffIds2`.
- Conditions override `EvaluateCore` and state the positive rule only; the base applies the
  `wf_cnd_not_*` negation and the client invert switch.
- `IWiredContext.GetSelection(box)` is the only way a box resolves its inputs. It is synchronous
  because it only reads room state; never reintroduce an async twin or block on a task
  (`GetAwaiter().GetResult()`) inside a grain. When a synchronous caller needs a module answer,
  give the module a synchronous core (`CanPlaceFloorItem`) and let the async method wrap it.
- Wired time is counted in half-second pulses. Convert through `WiredPulses`; do not write the
  500 again.
- Anything that happens over time is an event the system consumes: signals
  (`WiredSignalEvent`, antennas are picked furni), stack calls (`WiredStackCalledEvent`), clocks (`WiredClockTickEvent` from
  `FurnitureCounterClockLogic`), games and scores (`RoomGameSystem` events), variable writes
  (`WiredVariableChangedEvent`), avatar actions (`AvatarPerformsActionEvent`) and item use
  (`RoomItemUsedEvent`). Periodic and "at given time" triggers are paced by
  `RoomWiredSystem.Timers` from the tick, not by an event.
- An event, a timer firing or a delayed continuation is one `WiredExecution`. Signals and stack
  calls sent inside it run inside it at once (receivers' selectors and conditions read the room
  as sent), each stack at most `WiredConfig.MaxStackVisitsPerWindow` signal or call runs per
  500 ms window; a delay starts a new execution. Every stack run and action is charged to the room's decaying usage
  (`ExecutionCost*`); past the cap the execution is cancelled and EXECUTION_CAP counted. Heavy,
  DELAYED_EVENTS_CAP, RECURSION_TIMEOUT and KILLED are their own settings; do not add depth or
  per-tick hop limits.
- Movement of furni and users goes through `IWiredExecutionContext` (`ProcessFloorItemMovementAsync`,
  `ProcessUserMovementAsync`, `ProcessUserDirectionAsync`); the system flushes one
  `WiredMovements` packet per action. A furni mover derives from `FurnitureWiredMoveActionLogic`
  and only plans (`PlanMoves` on a `WiredMovePlan`): it reads positions through `plan.Where`,
  rolls its dice there, and holds every side effect back with `plan.Then`. "Can Perform
  Movements" (`wf_cnd_valid_moves`) is why: before the conditions are asked, the system picks and
  orders the effects that would run and plans their moves on one `WiredGhostLayout`, so a furni
  moved twice is checked from where the first move leaves it, and the moves then made are the
  checked ones (each re-checked against the room when made, for delays). Effects run in a random
  order unless "Execute In Order" sets `IWiredPolicy.ExecuteInOrder`. Placement rules live once,
  in `RoomMapModule.EvaluateTile` and `CanPlaceFloorItem`; a ghost reads them through
  `IRoomTileView`, never through a copy. Text an action shows goes through `FormatTextAsync` so the
  placeholder addons apply. Selections carry player ids and furni object ids; bots are not in
  them, bot actions resolve the named bot from their string param.
- **An internal variable's place in the editor's list is its id.** The client sorts by variable
  id, and `WiredVariableIdBuilder.CreateInternalOrdered` builds one from the target's band, the
  box's `SubBandType` and its `Order`, highest first. So the order is declared, never
  incidental: `python scripts/wiredvars.py` prints the list as the editor shows it. Leave gaps
  in `Order` for what is not written yet, because a variable's id is what a saved box stores
  and renumbering one loses every reference to it. The furni and user lists are numbered to a
  layout given in full, and the slots of the variables below are reserved and deliberately
  empty.
- **A variable with nothing behind it is not declared.** These have no system yet and are
  reserved rather than written: user `@level` (the avatar level, which this hotel does not
  have).
- **What a variable reads is on the avatar before it is asked for.** A variable is
  synchronous and must never await a grain, so anything an account owns is put on the avatar
  when it enters (`RoomAvatarModule.LoadBadgesAsync`, `LoadHabboClubAsync`) and pushed again
  when it changes. `PlayerSummarySnapshot` is not the way to carry it: that call is on every
  hot path. **Store the moment, not the verdict, where one exists.** `@is_hc` reads
  `IRoomPlayer.HabboClubExpiresAt` and compares it against now, so a membership running out
  while its owner stands in the room needs nobody to notice and no timer to run; only a
  purchase is pushed (subscription grain → presence → room).
- **A user variable is keyed by the avatar's room index, never by the player.** That index is
  how the client addresses any avatar and the only id a player, a pet and a bot all have; a
  player's own id is a value a variable reports (`@user_id`), exactly as `@pet_id` and
  `@bot_id` report theirs, so keying by it would confuse the address with the answer. Every
  path agrees on this: the menu passes the index the client sent, `GetTargetIds` looks the
  index up for each selected player, the fx system keys by `holder.ObjectId`, and
  `PlayerActiveStore` keeps one store per avatar and drops it on `PlayerLeftEvent.ObjectId`.
  A subclass of `UserVariable<IRoomAvatar>` then answers for all three kinds, one of
  `UserVariable<IRoomPlayer>` only where a player is meant, and `@pet_id` / `@bot_id` take
  `IRoomPet` / `IRoomBot`. Indices are handed out by a counter that only climbs while the room
  is loaded, so one is never two avatars; a player who leaves and returns is a new avatar and
  holds nothing of what the old one did.
- **Wired says "user" and means any avatar.** `IWiredSelectionSet.SelectedAvatarIds` holds
  players, pets and bots alike, by room index, and every selector gathers all three; narrowing
  to one kind is what `wf_slc_users_bytype` is for, and its mask (1 players, 2 pets, 4 bots, the editor's `usertype.1/.2/.4`)
  is honoured. A box reads the selection through `GetAvatars` when what it does suits any
  avatar (move, teleport, freeze, direction, hand item, a name in text) and through
  `GetPlayers` when only a player can be the subject (kick, mute, a badge, a team, a score);
  `GetPlayers` is `GetAvatars` with the others dropped, so a pet in the selection is skipped
  rather than mistaken for a player. Whatever names a player by player id — an event, a game
  team, a signal — is looked up and put into a selection as that player's avatar.
- **A trait every avatar can hold belongs on `IRoomAvatar`, implemented once on `RoomAvatar`.**
  Posture, hand item, freeze, idle, dance and effect are all declared there, and `RoomAvatar`
  carries the one implementation; a kind-specific interface declares only what that kind alone
  has (`PlayerId`, `BadgeCodes` and `AchievementScore` on a player, `Skills` on a bot, the pet's
  stats). A trait declared twice, once on `IRoomPlayer` and once on `IRoomBot`, is drift: the two
  copies drifted apart exactly that way, the player's `SetDance` refusing to *stop* a dance while
  seated and the bot's forgetting to mark the snapshot dirty. The test is what the client draws,
  not what the hotel calls a user: `RoomObjectUserTypes.getVisualizationType` maps `bot` and
  `rentable_bot` to `"user"`, so a bot dances and wears effects exactly as a player does, while a
  pet keeps its own visualization and only `AvatarLogic` reads the effect update — a pet therefore
  holds the value and shows nothing, which is a no-op and not a special case to write. A dance and
  an effect are on `RoomAvatarSnapshot` for the same reason, so room entry replays both for every
  avatar in one pass instead of branching per kind.
- **An event about an avatar names it by room index, not by player id.** `AvatarEvent` derives
  from `RoomObjectEvent` and so carries `ObjectId`, which is the address wired already selects
  and stores by; `PlayerEvent` carries a `PlayerId` and is for what happens to the *account*
  behind an avatar (entering, leaving, chatting, a controller level changing). An action —
  waving, dancing, sitting, a sign — is the avatar's, so `AvatarPerformsActionEvent` is an
  `AvatarEvent`: a bot or a pet reports it exactly as a player does, and nothing has to invent a
  player id it does not have. `CausedBy` still names the player when there is one and is the
  room's own system context otherwise. `SeedSelectionFromEvent` resolves a `PlayerEvent` through
  `AddPlayerById` and any `AvatarEvent` through `AddAvatarByObjectId`, with a general
  `case AvatarEvent` last so a new avatar event needs no wiring; the two walk-on/off cases sit
  above it only because they add their furni as well. Prefer an `AvatarEvent` for anything a bot
  or a pet could also do, and keep a `PlayerEvent` for what only an account can.
- **How a player got into the room is the room's to remember, not the client's.**
  `OpenFlatConnection` carries no reason (its third field is always -1), so a furni that sends
  someone elsewhere says so first: it puts a `RoomEntrySnapshot` on their presence against the
  room it is forwarding them to (`SetPendingRoomEntryAsync`), and the room applies it as the
  avatar is made and drops it. It is kept against one room id, so a forward the player never
  followed cannot colour a later entry, and anything else is `RoomEntryMethodType.Default`.
  `wf_act_teleport_to_room` tells the two apart by the `RoomLinkerData` it followed: a fixed
  `RoomId` is a room network, a paired `ItemId` is a teleporter and the player arrives at that
  half, which is what `@room_entry.teleport_id` reports.
- **A text connector carries a word, not a key.** The editor writes it into its table as
  `12 (Bubble Juice)`, and the client only localises a string that *begins* with `${`, so a key
  would be shown as a key. The hotel's own texts come from the client's `ExternalTexts.json`,
  named by `Turbo:Texts:ExternalTextsPath` — either a path on disk or an `http`/`https` address,
  because the file is usually served beside the rest of the client's assets. It is empty by
  default, in which case the editor simply shows the number. `IHotelTextProvider.ReloadAsync`
  reads it, called from `TurboEmulator.StartAsync` with the other providers rather than from a
  constructor, so the fetch is awaited and a hotel that names something unreadable is logged and
  started anyway: no names is cosmetic, refusing to boot is not. How long to wait on the address
  is `Turbo:Texts:FetchTimeoutSeconds`, because a timeout is a setting and not a constant. The
  request sends a `User-Agent`
  because asset hosts answer 403 to a request that names none, and `HttpClient` sends none of its
  own. `WiredTextConnectors` names hand items
  (`handitem{id}`), effects (`fx_{id}`), dances and signs by the client's own keys, following a
  text that is only a reference to another one. The provider sits in the room module because
  wired is its only caller; move it when a second module wants texts.
- **A variable with no value is held or not, never a zero.** `WiredVariableFlags.HasValue` is
  what says a variable carries a number. Without it the client only draws who holds one, and
  "has variable" is the only question anything asks of it, so an avatar or furni it is false
  for must answer that it holds nothing. `@is_hc`, `@has_rights`, `@is_owner`, `@is_frozen`,
  `@is_muted`, `@is_trading`, `@is_idle` and `@projectile.animation.is_traveling` each reported
  `0` instead, which put every avatar and every furni in the room on the holder list and left
  each flag saying nothing at all. The user and furni families answer this the same way and
  have the same three shapes: `TryGetValueForAvatar` / `TryGetValueForItem` for a variable that
  can be absent, `UserValueVariable` / `FurnitureValueVariable` for one that every target it
  binds to holds, and `UserFlagVariable` / `FurnitureFlagVariable` for one that is held or not.
  A flag declares no `Flags` of its own, because `WiredInternalVariable.Flags` is already
  `None`. A shape added to one family belongs on the other in the same change; `AGENTS.md` used
  to say the two differ here on purpose, and the seven user flags are what that cost.
- **A projectile's flight is followed, not guessed.** The server moves furni at once and the
  client animates over the animation time it was sent, so `WiredProjectileFlight` follows that
  same clock to answer the `@projectile.animation.*` variables: where the furni looks to be,
  how far it has come, whether it is still going. The clock is the client's own; what counts
  as a collision is this server's reading, recorded on the type. A flight outlives its landing
  so a stack can read how the shot went, and is dropped when the furni leaves the room.

- User variables are keyed by the avatar's room index, like everything else wired says about an
  avatar. Every walk over "who holds a variable" goes through
  `RoomWiredSystem.GetLiveTargetIds(targetType)`: the "users with variable" selector yielded
  player ids from its own enumeration and so found nobody. Derived variables (level-up, time
  utility) implement `IWiredSubVariableProvider` on the variable box tile and are rebuilt with
  the variable boxes. Value timestamps live in `KeyValueStore.Timestamps`
  (`Turbo.Rooms/Wired/Storage/`).
- Freeze effect ids are `WiredConfig.FreezeEffectIds`; the list ships as zeros and is hotel
  data. Team effect ids belong to the game, not to wired: `RoomConfig.GameTeamEffectIds`.
- Everything in a wired save comes from a client and is checked before it is stored, in
  `FurnitureWiredLogic.ApplyWiredUpdateAsync`; the same checks run again when a box loads, and
  stored ints that fail them are replaced by the box defaults. A new box gets this for free as
  long as it declares its inputs honestly:
  - One rule per int param, the narrowest that fits: `WiredEnumParamRule<T>`,
    `WiredBoolParamRule`, `WiredRangeParamRule`, or a shared one from `WiredRules`
    (`VariableTarget`, `HandItem`, `Effect`, `TileOffset`, `NonNegative`). `WiredRules.AnyInt`
    is only for values with no bound at all (half of a 64-bit value, a bit mask, a variable's
    value). Never cast an unchecked int to an enum, and never use a client int as a loop bound
    or an array index; rectangles go through `WiredArea`, which cuts them to the map and to
    `WiredConfig.SelectorMaxAreaSize`.
  - Text is cut to `GetStringParamMaxLength()` and stripped of control characters on save.
    Override it with the client's own input limit for the box (`TextInputParam`/`TextAreaParam`
    in the AS3 editor class) instead of truncating at execution time. Text that is expanded
    (`FormatTextAsync`) is capped again after expansion. A regex built from player text is
    escaped and run with a match timeout.
  - Picked furni must be in the room, are de-duplicated and capped by
    `WiredConfig.SelectedItemsLimit`; variable ids must parse (`WiredVariableId.TryParse`), exist, and
    fit `GetMaxVariableIds()`; sources outside `GetAllowed*Sources()` fall back to the default.
  - Malformed input is refused or dropped without throwing. Do not catch an index or parse
    exception to detect it: a client could fill the log one request at a time.
  - Stored data is as untrusted as sent data, and a box that was never saved stores nothing.
    `FillInternalDataAsync` rebuilds every list to the shape the box declares, so code after
    it reads by index without a `try`: int params by their rules, and the definition and type
    specifics by `NormalizeSpecifics` (one entry per declared type, the default where the
    stored one is missing or does not fit). `List<object>` comes back from JSON as
    `JsonElement`, never as the `int` or `bool` that was saved; the base class used to index
    and cast inside a `catch`, which logged an exception for every freshly placed action,
    condition and selector and dropped every delay, quantifier and invert switch on the first
    room load after a save. Sources are read through `StoredOrDefault` for the same reason.
    A new kind of stored list gets its normaliser in the base class, not a `try` at each
    reader.
- A box that creates value outside the room (`wf_act_give_reward`: badges, furniture) raises
  `MinimumControllerLevelToSave`, so holding the room's wired permission is not enough to
  configure it.
- The wired menu changes variables through two packets. `WiredSetObjectVariableValue` works on
  one furni or user that is in the room right now, and its last field says what to do
  (`WiredVariableMenuOperationType`: edit a value, give the variable, take it away); each
  operation asks for the flags the client checks before it shows the button (`HasValue` and
  `CanWriteValue` to edit, `CanCreateAndDelete` to give or take), because the client is not
  trusted to have checked. `WiredDeleteAllVariableHolders` takes a stored furni or user
  variable from every holder the box keeps, present or not
  (`FurnitureWiredVariableLogic.RemoveAllValues`), and is logged. Both go through
  `RoomWiredSystem`, remove through `RemoveValue` so triggers and fx hear of it, and answer
  with the refreshed list the tab is showing.

- **A box's save packet is read from the client's composer, never mirrored from what the server
  sends it.** The two shapes differ: the client sends back only what the player can change.
  A condition's save carries one definition specific (the quantifier the player picked) and no
  type specifics, while the def the server sends carries the quantifier *type* and the invert
  flag as well. Reading those two back misaligned every condition save from the byte after the
  variable ids. A value the client only ever draws is declared by the box, not stored with it:
  `FurnitureWiredConditionLogic.QuantifierType` and `IsNegative()` feed
  `GetTypeSpecifics()` directly. Today only actions (the delay), conditions (the quantifier)
  and selectors (filter and invert) send a specific at all, and none of them sends a type
  specific.
- **A wired code is checked against the client's code table, both halves.** An editor class
  declares its own `code` and its negative twin's `negativeCode`, so `wf_cnd_not_wearing_b`
  has a code of its own (22) and must not reuse the positive box's (11), or the client opens
  the wrong editor. `scripts/wiredgap.py` reads both getters and reports any code two boxes
  claim; a second furni that is genuinely the same box inherits its twin instead of declaring
  the code again (`WiredTriggerAtTimeLong`).
- **An option whose editor greys out an input does not need that input.** The "says something"
  trigger's third mode is "Match all text": the client disables the keyword box, so the trigger
  fires on anything said. Reading it as "every word of the keyword must appear" meant it never
  fired, because the keyword is empty.
- **A sentinel is a value the rule has to allow.** The condition-evaluation addon writes -1 in
  its mode param when the player picks one of the counted modes, and puts which one in the next
  param and the number after that. A rule of `0..6` refused the save outright, and the modes
  were read one slot early. Check an editor's `readIntParamsFromForm` for a branch that
  rewrites a param before trusting the obvious reading of it.
- **A request that is not a save is not answered with a save.** `WiredSaveSuccess` closes the
  editor, so "apply furni to set conditions" (`ApplySnapshot`, which re-captures a box's furni
  snapshot through `FurnitureWiredLogic.ApplyFurniSnapshot`) answers nothing at all: the client
  expects no reply and keeps the editor open.
- Which boxes the server still lacks is measured too: `python scripts/wiredgap.py` (the
  deobfuscated client is found through `HABBO_CLIENT`) lists every wired code the client has an
  editor for and no box returns. The client knows codes, never furni names; the furni a code
  belongs to is looked up in the hotel's `furniture_definitions`. Do not write a box for a code
  no furni in the hotel carries: nobody can place it, and its param layout goes untested.
  Still missing after this pass, each for want of a system rather than a box: the chest item
  type scanner (addon 18, which writes a context variable, and context variables hold no
  value yet), reward tracks
  (58, 59), achievements (the enabler addon 2001 and action 51, which progresses one) and the
  web API addon (2002). Give effect (52), override height (53), the level condition (44), the
  global placeholder (2000) and variable box 8 have no furni in the hotel.
- What an editor class does not say is in two other places, and both are read before a box is
  written or called unknowable. The hotel's texts and furnidata
  (`nitro-assets/gamedata/ExternalTexts.json`, `FurnitureData.json`): the `usage_info` and
  option texts say what a box does, and the furni's display name says which code it is.
  `wf_xtra_rotate_to_dir` is "Projectile" (21) and `wf_xtra_mov_curve` is "Movement Curve"
  (22, the code table's JUMP_STRENGTH); neither name gives it away. And the editor's pictures:
  the projectile's four directional systems are explained only by
  `wired_misc_directional_system_N.png`, and `WiredDirections` was read off those tile by tile.
- The projectile addon is partly done, and says so on the class: turning a projectile to its
  flight direction, a curved trajectory and overshooting are in; scaling the animation time
  with distance, the shooter's cosmetic direction and bunny hop, and the seven internal
  variables are not, because nothing the server can read says how they work. Its nineteen
  params are all declared anyway, so a save comes back as it was made. It speaks only for the
  furni it picked as projectiles (`WiredProjectileSettings.ProjectileIds`), unlike the rest of
  the policy, which covers the whole stack.
- Variable ids that mean something by position (fx: override min, override max, audience;
  place furni: spawn variable, value variable; projectile: time, distance) set
  `HasPositionalVariableIds`. The base then keeps every slot and stores "none" as id 0; without
  it an unknown id is dropped and the next variable slides into its place.
- A movement's arc is the stack's, not the action's: `wf_xtra_mov_curve` (the client's "jump
  strength") sets `IWiredPolicy.JumpStrength`, and `WiredExecutionContext` writes it into every
  move of the stack as the jump power of a user and the curve strength of a furni. An action
  never sets either.
- A value-or-variable input is read one way: the switch as a bool, then the literal, or the
  variable through `TryReadVariableOperand` (the first selected target that holds it).
  `TryResolveOperand` is that plus a literal stored as a long; a box whose editor stores one
  int reads the literal itself (`WiredAddonJumpStrength`).
- What a box does only in the client is sent to the selected players and nothing is kept:
  `wf_act_click_conf` sends `WiredClickSettings`, the client applies it and forgets it on
  leaving the room. It goes out with `LogAndForget`, like everything a box sends from the tick.
- Two placeholder addons that differ in whose names they print share
  `FurnitureWiredNamePlaceholderLogic`. A furni's name on the server is its definition name;
  the localised name is the client's.

## Variable fx
- A variable fx draws a wired variable over whoever holds it: a bar, hearts, a level badge or a
  number over avatars (user fx) or floor furni (furni fx). One addon per category
  (`wf_xtra_var_fx_health`, `_progress`, `_level`, `_status`, `_boss`, `_number`; wired codes
  1200 to 1205), all deriving from `FurnitureWiredVariableFxLogic`. The logic names are this
  server's and differ from the furnidata names (`wf_xtra_varfx_hp`, `_prog`, `_levelling`,
  `_status`, `_boss`, `_number`), unlike every other wired box, whose logic is its furni name;
  the `logic` column of those six `furniture_definitions` rows has to be set to match.
- An fx shows nothing until three things hold, and none of them is a packet: a variable box
  stands on the fx box's own tile, the box's source (user or furni) is what that variable
  hangs on, and someone in the room actually holds the variable. An fx stacked on a trigger,
  or standing alone, is configured correctly and draws nothing.
- The editor never picks the variable an fx shows, only the two that may replace its value
  range and the one that gates its audience. Which variable it shows is the room's rule: the
  variable box on the same tile (`GetVariableBoxOnTile`, shared with the sub-variable addons),
  and only when that variable hangs on what the fx is drawn over. An addon that needs another
  kind of neighbour on its tile asks `FurnitureWiredAddonLogic.GetLogicOnTile<T>()`; do not
  write the tile walk out again. A levelling fx takes level,
  level cap and the experience bounds of the current level from the level-up addon on that
  tile (`WiredAddonVariableLevelUp.TryGetLevelProgress`).
- The box holds no state and sends nothing. It answers three questions: how the fx looks
  (`BuildConfig`), what one holder shows (`ResolveStatus`) and who may see it (`Visibility`).
  `RoomVariableFxSystem` (`Turbo.Rooms/Wired/VariableFx/`) does the rest: after something
  changed it works out, per player, every status that player should be seeing, compares it
  with what they were sent and sends the difference as one ordered batch. Do not send an fx
  message from a wired box or on a variable write; mark the system dirty with an event.
- The client filters nothing, so audience is decided here (`VariableFxVisibilityType`): only
  the holder, the holder's game team, everyone, or viewers who hold a user variable (with a
  given value). It also keeps nothing across rooms: configs and statuses go out again on every
  entry, configs first.
- What the client does not check, the server must get right, because the failure is an
  exception in the client, not a missing bar. All of it was read from the AS3 and is kept in
  `VariableFxStyles` / `VariableFxIcons`, the team colours of the "delegated colour" included
  (`VariableFxStyles.GetTeamColor`); the system and the boxes hold no client table of their own:
  - a (category, style) pair outside the style table loses the whole config message;
  - a renderer must be one the style registers, a number display needs its `design` extra, an
    icon must be one the client has an asset for;
  - every status of a levelling fx, and of any fx using the levelling colour, must carry
    `is_maxed` (with `current_level` and `max_level`);
  - the two ends of a range override travel as a pair, so one overridden end repeats the
    default for the other; a maxed level sends both ends equal with `is_maxed`, which is how
    the client knows to draw the bar full.
- The client drops a status for an avatar or furni it has not been told about, and never asks
  again. Those objects reach it over the room stream while fx messages go to the player
  directly, so a new player, avatar or furni waits `WiredConfig.VariableFxEntryDelayMs` before
  anything about it is sent. A viewer's first batch, and a holder's first status, are marked
  "initialize": the client then draws the value without the change animation.
- The three variable ids of an fx box are positional (override min, override max, audience).
  `GetValidVariableIds` keeps the slots and stores "none" as id 0, which `WiredDataSerializer`
  writes as an empty string; the default implementation drops unknown ids and would slide the
  audience variable into the first slot. Any box with positional variable ids needs the same.
- Limits are `WiredConfig.VariableFx*`: boxes per room (enforced through
  `IRoomPlacementLimit`), statuses per viewer, the flush interval and the entry delay.

## System boundaries: wired is a user of the room, not a part of it
Wired touches everything, so it is where systems bleed into each other first. These rules came
out of pulling it apart; they hold for any system that grows the same way.
- **Wired lives in its own tree.** `Turbo.Rooms/Wired/**` (runtime, rules, variable storage),
  `Turbo.Rooms/Object/Logic/Furniture/Floor/Wired/**` (every `wf_*` furni, the counters
  included), `RoomWiredSystem*.cs`, `RoomGrain.Wired.cs` / `IRoomGrain.Wired.cs`, and the
  `.../Wired/` folders in `Turbo.Primitives`. A wired-only type anywhere else is misplaced,
  however neutral its name: the variable stores sat in `Grains/Storage`, two wired events in
  the events root, the wired grain methods in the floor-furniture partials.
- **Wired settings are `WiredConfig`** (`Turbo:Wired`), and only what wired code reads goes in
  it. A `Wired` prefix is not a reason: the bot follow distance (`BotConfig.FollowDistance`),
  the game duration and the team effect ids (`RoomConfig.Game*`) were all named `Wired*` while
  wired never read them.
- **A general module does not recognise a wired box.** No `is IWiredBox`, no
  `is FurnitureWiredLogic`, no wired error code outside the wired tree. When general code needs
  wired to have a say, it offers a seam that does not name wired and wired plugs into it in
  `RoomGrain`'s constructor, the one place that knows every system:
  - refusing a placement: `IRoomPlacementLimit`, asked through
    `RoomFurniModule.EnsureWithinPlacementLimits` (the wired box cap);
  - reacting to a change: a room event. `RoomSecurityModule` publishes
    `PlayerControllerLevelChangedEvent`; the wired system answers it with the wired permissions.
    The security module and the presence grain used to compute and carry `canModifyWired` /
    `canReadWired` themselves.
- **What wired drives is not wired's.** Teams, scores and the running game are
  `RoomGameSystem` (`GameTeamType`, `GameStartedEvent`, `GameEndedEvent`,
  `GameScoreChangedEvent`, `GameTeamChangedEvent`): a counter furni starts a game, wired boxes
  join teams and give points, wired triggers listen, and banzai or freeze furni will use the
  same system. Joining or leaving a team is also what tells the client it is playing
  (`YouArePlayingGame`), so that is sent from the game system and nowhere else; the variable fx
  system learns of team changes from the event, not from whoever moved the player. Frozen is
  avatar state (`IRoomAvatar.SetFrozen(isFrozen, thawsOnTeleport)`), not a set kept by whoever
  froze it; that set was never cleared when the avatar left.
- **A wired action asks the owning module; it does not redo the module's work.** A box picks
  the targets and the parameters, then makes one call: `RoomModerationModule.KickPlayerBySystemAsync`
  / `MutePlayerBySystemAsync`, `RoomChatSystem.WhisperToPlayerAsync`,
  `RoomFurniModule.MoveFloorItemAsync`. When the module has no such method, add it to the module
  (`...BySystemAsync` for "no actor, no rank check") and call that. The copies had already
  diverged: the wired kick protected only the owner while the wired mute protected staff too,
  and wired moved furni straight on the map, so `OnMoveAsync` never ran and a roller or wired
  box moved by wired kept its old tile in the roller index and the wired stacks.
- **One system may batch what another announces, not skip what it does.** Wired sends the moves
  of an action as one `WiredMovements` packet, so it passes `announce: false`; the map update and
  the logic callback still go through the furni module.
- **Wired reads the room through the modules, not through `_state`.** The avatar and furni
  modules have a read surface for everyone who is not them: `RoomAvatarModule.Avatars` /
  `Players` / `TryGetAvatar` / `GetAvatarsOnTile` / `HasAvatarOnTile` / `GetAvatarsOnItem` /
  `TryGetNearestPlayer`, `RoomFurniModule.Items` / `TryGetItem` / `TryGetFloorItem` /
  `GetFloorItemsOnTile` / `IsHighestOnTile`, `RoomMapModule.IsTileDisabled`. They are
  bounds-safe, so a caller does not repeat the `InBounds` check or index a tile array. The only
  `_state` members wired code still touches are `RoomSnapshot` (its own masks and timezone) and
  `NextWiredBoundaryMs` (set with the other tick boundaries). Before reading `_state` from
  outside the owning module, look for the accessor; when it is missing, add it to the module.
- **Putting an avatar somewhere is `RoomAvatarModule.RelocateAvatarAsync`.** It stops the walk,
  tells the furni left and the furni landed on, moves the avatar on the map and fixes its
  height. Wired teleports and carried avatars use it; the walked step in `RoomAvatarTickSystem`
  shares its `NotifyWalkOffAsync` / `NotifyWalkOnAsync`. Whether the avatar may stand there, and
  how the room is told (wired batches a `WiredMovements` packet), stay with the caller. A
  `RemoveAvatar` / `SetPosition` / `AddAvatar` sequence anywhere else is drift: six copies (bot
  and pet moves, bot teleport, mount, dismount, ride sync) skipped the furni notifications and
  the invoke. The ridden pet passes `notifyFurni: false` so one step does not trigger twice.
- **Which tile is "toward" or "away from" something is the map's** (`RoomMapModule.GetStepsToward`
  / `GetStepsAwayFrom`, straight steps, longer axis first). The chase and flee boxes used to do
  it with index arithmetic of their own, each differently. A box decides what the furni wants;
  the map says where that is; the furni module says whether it fits.
- **A bubble nobody typed is `RoomChatSystem.SayAsAvatarAsync`** with an `AvatarSpeech` (style,
  shout, whisper to one player, width): bot lines and orders, pet replies, the wired message
  box, the word a player gets before a wired kick or mute. Six places built those composers by
  hand, and only some of them filtered the text; the chat system filters all of it. It is
  deliberately not chat: no flood check, no commands, no chat log, no `PlayerChatEvent`.
- **A general event speaks the room's language, the listener translates.**
  `AvatarPerformsActionEvent` carries an `AvatarActionType` and the expression, dance, sign or
  posture it was; `WiredAvatarActionMatcher.TryTranslate` turns that into the wired editor's
  own numbering. The avatar module used to publish `WiredAvatarActionType` and map expressions
  onto it, with anything unknown counted as a wave.
