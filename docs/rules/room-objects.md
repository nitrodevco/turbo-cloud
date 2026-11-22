# Room Object Logic Rules

- [Logic classes](#logic-classes)
- [Furni that gives, rents or plays something](#furni-that-gives-rents-or-plays-something)
- [Teleporters](#teleporters)
- [Temporary furni](#temporary-furni)
- [Avatar effects](#avatar-effects)
- [Pets and bots](#pets-and-bots)
- [Room runtime](#room-runtime)

Rules for furni logic under `Turbo.Rooms/Object/Logic/`, the furni that give, rent or play
something, teleporters, temporary furni, avatar effects, pets and bots, and the room's runtime.
Wired has [a page of its own](wired.md); inventory, badges and trading are in
[Inventory Rules](inventory.md). [Furni Logic & Room Turns](../furni-logic.md) is the tour for
plugin authors.

## Logic classes

- Behaviour lives in a `[RoomObjectLogic("<type>")]` class under `Turbo.Rooms/Object/Logic/`; the
  type name must equal the `furniture_definitions.logic` value, or the provider falls back to
  `default_floor` and logs a warning. Never special-case a furniture type in a handler or module.
- A client action with its own packet (dice, dimmer preset, mannequin outfit, love-lock answer)
  is a `FurnitureInteraction` record under `Turbo.Primitives/Furniture/Interactions/` carrying
  that packet's payload, routed through the single `IRoomGrain.InteractWithItemAsync`; the logic
  pattern-matches it in `OnInteractAsync` and checks its own permissions with `HasRightsAsync`,
  `IsItemOwner` (the item's owner only — for turning an item into value: exchange, present, pet
  package, seed) or `IsItemOrRoomOwnerAsync` (for changing how it stands: mannequin, trophy),
  refusing through `Reject(...)` so the refusal is logged with ids. A plain
  double-click stays on `OnUseAsync`. Do not add a grain method per furniture type.
- Replies that go to one player (a preset list, an unwrapped gift) are sent by the logic via
  `SendComposerToPlayerAsync` (the grain factory extension); room-wide changes go through
  `SetLegacyDataAsync` / `SetNumberDataAsync` /
  `SetStringDataAsync` / `SetMapDataAsync`, which persist and refresh in one step.
- The exact data shape each widget needs (dimmer `state,preset,effect,#RRGGBB,brightness`, toner
  `[state,h,s,l]`, love lock string array, trophy tab-separated) comes from the Flash client's
  logic classes; the constants next to each logic record the format, so verify against the client
  before changing one.
- Delayed item work (a dice landing, a door closing) is scheduled on `RoomTimerSystem`, keyed by
  the item, and cancelled in `OnPickupAsync`. Never `Task.Delay` inside a grain turn.
- A table of protocol state values the client interprets lives as a static class under
  `Turbo.Primitives/Furniture/` (`DiceStates`, `WheelStates`, `DimmerStates`, `StickieColors`,
  `RentableSpaceStates`, `GateStates`), never as `private const int STATE_*` in the logic class.
  What makes it a table rather than a local constant is that a second reader exists or could:
  `GateStates` was two private copies of the same `CLOSED = 0 / OPEN = 1` in the gate and the
  one-way gate, which is how a table announces itself. A single logic's own field offsets and
  ranges (`STATE_INDEX`, `MIN_CHANNEL`, a wired box's `PARAM_*`) stay beside the class that
  reads them, because there is nothing to share and the doc comment above them records the
  format. Durations and limits are `RoomConfig` tunables either way.
- Validate client data in the action module before the logic sees it: colour must be in the
  palette, text within `StickieTextMaxLength`, map entries within the `ObjectData*` limits.
  Reject with a `LogWarning` naming the item, room and player.

## Furni that gives, rents or plays something
- `clothing_change` (a booth that dresses whoever walks in), `rentable_space` and `youtube` (a
  video display) are ordinary `[RoomObjectLogic]` classes whose client packets arrive as
  `FurnitureInteraction` records, like the dice and the dimmer.
- A room never awaits a change to a player's figure. `RoomAvatarModule.ChangePlayerFigure`
  tells the player grain with `LogAndForget`: the player grain tells the presence, the presence
  comes back to this room to update the avatar, and a room waiting on that waits on itself.
  The mannequin awaited it and would have hung for the grain timeout on every use.
- A furni used up for what it gives (credit furni, a crackable, a pet product, a present) goes
  first and pays second: `RoomActionModule.DeleteItemByIdAsync` writes the item's destruction
  before it returns and throws when that write fails, and only then does the caller grant. A
  credit is paid under a reference of the item's own (`FurnitureExchangeLogic.CreditReference`),
  so a repeat pays nothing, and a grant that is refused gives the item back
  (`GiveBackUsedItemAsync`). Paid first, a delete that failed or a crash in between left the
  item to be used again.
- A furni that lets a player build without room rights implements `IRoomBuildArea`; the
  placement path asks through `RoomFurniModule.HasBuildAreaRights` once room rights said no,
  for a new item and for moving one's own item inside the area. It does not check for a rentable
  space. A rent lives in the item's extra data (`RentableSpaceData`), ends on a
  `RoomTimerSystem` timer that is set again in `OnAttachAsync`, and ending it sends what the
  renter built there home. The status message reuses the refusal enum, and the client reads
  `RentableSpaceRentFailedType.None` there as "can rent".
- Many items leave a room through `RoomActionModule.ReturnItemsToOwnersAsync`: the room lets go
  of all of them, tells the client once per owner (`ObjectRemoveMultiple`) and hands each
  owner's inventory its share in one call. Room deletion and an ended rent both use it.
- A player may always pick up their own furni, whatever their rights
  (`RoomActionModule.RemoveItemByIdAsync`); otherwise a renter, or a guest whose rights were
  taken, could never get it back.
- A video display keeps a clock, not a stream: which video, how far in, paused or not. The
  client plays it and never says when a video ends, so each video's length is config
  (`RoomConfig.YoutubePlaylists`) and the room moves on by timer.
- A jukebox keeps a clock the same way: the client plays each song and never says when it ends,
  so the room says which disk plays and how far in (`NowPlaying`) and moves on by timer. Its
  disks are `IJukeboxGrain`'s, held as their owners' rows through the wired chest's holder column
  in `held_position` order; songs are `ISongDirectoryGrain`'s. The jukebox packets name no item,
  so they go to `IRoomGrain.InteractWithMusicPlayerAsync`. A song disk is the trax song category
  (`SongDisks`), its song id its legacy stuff data, and the client reads that id only from the
  item's extras. This client has no trax editor, so nothing composes or burns songs
  ([Songs and jukeboxes](../sound.md)).
- A post-it pad is a stack of sheets: its legacy stuff data is the count (`PostItPads`), bought
  as the product's quantity when above one, else `InventoryConfig.PostItPadSheets`. `PlacePostIt`
  and `AddSpamWallPostIt` name the player's pad, not a room item, so they go to
  `IRoomGrain.PlacePostItAsync` / `AddSpamWallPostItAsync`: the room checks rights and the spot,
  takes a sheet (`IInventoryGrain.TakePostItSheetAsync`: `PostItPlaced`, or the pad used up
  with its last), makes a note of the pad's own kind owned by the room's owner, and gives the
  sheet back when no note came of it. Without building rights a placement in a room with a
  post-it wall is answered with the note editor (`RequestSpamWallPostIt` with the pad's id); the
  wall itself does nothing on a double-click.
- Which client packets the server still lacks is measured, not guessed:
  `python scripts/packetgap.py Room` (nitro-next is found through `NITRO_NEXT`) compares the
  composers nitro-next constructs and the messages it listens to with the handlers that do
  something and the composers something sends. Its "stub" and "unsent" are heuristics, so read
  the file before acting on a line. What it still lists, and why, is kept in
  [docs/room-gaps.md](../room-gaps.md): rented furni, the room queue and spectators, the
  hoppers and VIP gates, and `UseObject` and `BotSkillListUpdate`, whose composers are complete
  but which nothing known causes. `SpecialRoomEffect` is sent by the `:roomeffect` room command.
- The configuration furni (`FurnitureConfigurationControlLogic`: the Invisible Furni
  Controller, the Handitem blocker and the `:chooser` disabler) each switch one flag of
  `ConfigurationItemStates`. The room is told whenever a switched-on one changes, arrives or
  leaves, and a player walking in only when one is on, because the client starts every room
  with all four off. Free furni movement has no furni and is sent as off.
- A furni that loses something when picked up says so through
  `IFurnitureLogic.GetPickupConfirmation` (a wired variable box: its stored values and every
  reference to it). The room then answers an unconfirmed pickup with `ObjectRemoveConfirm` and
  picks the item up only when the client sends the pickup again with its confirm flag set.
- A Room Area Hider hides furni by not sending them (`RoomFurniModule.AreaHide`): the client
  only takes the floor out of the area, and the area's tiles go -1 in the height map
  (`IsTileHiddenByArea`, refreshed by `RefreshAreaHiddenTiles`). Anything that sends a floor or
  wall item to the room asks `IsHidden` first, as placing and moving do, and a change to a hider
  or the Invisible Furni Controller is announced through `AnnounceHiddenItemsChangedAsync` (one
  ObjectRemoveMultiple / one Objects, as Habbo sends). The controller hides the invisible furni:
  hiders with "Make this furniture invisible" and the `RoomConfig.InvisibleFurniNames` kinds (the
  module keeps that flag as the clients were last told it, `RefreshInvisibleFurniAsync`).

## Teleporters
- A teleporter (`FurnitureTeleportLogic`, logic `teleport`) is half of a pair. The pair is made
  at purchase — one unit bought is two items, inserted, linked and committed in one transaction
  (`InventoryFurniModule.GrantTeleportPairsAsync`) — and each half names the other in its
  `room_linker` section by **item id only**. Where the other half stands is looked up when it
  is used (`RoomFurniModule.GetRoomIdOfItemAsync`), because either half can be picked up and
  placed anywhere.
- The sequence is Habbo's, one `RoomConfig.TeleportStepMs` step at a time, in the three states
  the client draws (`TeleportStates`: closed, open, flashing): used from anywhere, it walks the
  player to the tile in front; opens; they step in; it shuts; it flashes; then the other half
  in this room flashes, opens and lets them walk out, or they are forwarded to the other half's
  room. With no usable other half it opens again and lets them back out.
- Arriving from another room goes through `IRoomArrivalLogic`: the forward carries a
  `RoomEntryMethodType.Teleport` entry naming the far half, the avatar module starts the player
  inside it instead of at the door (a module asks the interface; it never names the furni), and
  the entry handlers let a teleport arrival past the door — doorbell and password, not bans or
  capacity (`IGrainFactory.PassesRoomDoorAsync`). A plugin's linked pair passes the door the
  same way when core finds it sending someone to its other half's room
  (`RoomEntrySnapshot.ThroughLinkedPair`), but it is no teleporter arrival: the arrival policies
  still place them and the entry method stays a plain one. The pending entry expires after
  `PlayerConfig.PendingRoomEntryTtlMs`, so a forward never followed cannot open a door later.
- One player at a time. While a teleporter holds someone, their own walk requests are refused
  (`IRoomAvatar.IsTeleporting`) without stopping the walk the teleporter has them on; every
  step re-checks the player is still where it left them and lets go the moment they are not
  (left, disconnected, moved by wired). Picking it up lets them go.

## Temporary furni
- A temporary furni is a floor item the room makes from a definition alone
  (`RoomFurniModule.PlaceTemporaryFloorItemAsync`): on the map, in the furni list players are
  sent, open to wired, and nowhere else. It has no row, no inventory and no persistence, and it
  is gone when the room unloads. `wf_act_place_furni` makes them from its saved snapshot of
  the picked furni (`WiredFurniSnapshotEntry.DefinitionId`), `wf_act_remove_furni` takes them
  away and touches nothing else.
- Its id is negative, handed out by the room counting down (`RoomLiveState.NextTemporaryItemId`),
  and `IRoomItem.IsTemporary` is just that sign. A negative id cannot collide with a row id,
  the client shows no infostand for one, and every packet handler already refuses ids that are
  not positive, so a player cannot pick one up, move it or trade it.
- Whatever writes items somewhere asks `IsTemporary` first: the dirty-item hook is not set
  (`RoomObjectModule.AttatchObjectAsync`), removal queues no delete, a pickup is refused and
  `ReturnItemsToOwnersAsync` lets them go without handing them to an inventory. A new path
  that persists or returns room items needs the same check.
- They cost nobody anything, so the room caps them (`RoomConfig.TemporaryFurniMax`); a stack on
  a repeater would otherwise fill the room. Reaching the cap or a blocked tile is not an error.
- The ids start again at -1 each time the room loads, so anything keyed by furni id that
  outlives the room must let go when one leaves: the wired system drops the stored variable
  values of a temporary furni on detach (`ForgetStoredValuesOfUnownedFurni`), or the next
  furni to get that id would inherit them.
- A new item has no logic until it is attached, and a check that type-tests `Logic` before
  then silently answers "not mine": the wired and variable-fx placement caps tested it and so
  refused nothing for as long as they existed. Placement calls `RoomObjectModule.EnsureLogic`
  before asking the limits; anything that runs earlier still falls back to the definition
  (`tItem.Logic?.CanWalk() ?? tItem.Definition.CanWalk`).
- **Placement is checked in the entry point, never at its callers.**
  `RoomFurniModule.PlaceFloorItemAsync` / `PlaceWallItemAsync` own the spot (`CanPlaceFloorItem`,
  one occupancy rule for new and moved items) and the limits. The limits used to be asked by two
  of four placement paths, so a Builders Club wired box skipped the cap.
- **A bounds question returns false; it does not throw.** `RoomMapModule.GetTileIdForSize` /
  `TryGetTileIds` answer by coordinate. It used to throw while its callers checked for `false`,
  and the wired movers guarded it three ways (a pre-check, a `try`, nothing).
- **"None" and a real id never share a value.** A tile with no top item said `-1`, and temporary
  furni ids count down from `-1`, so every empty tile named the first temporary furni. Use
  `RoomMapModule.NO_ITEM` (0) or a nullable.
- **Footprint maths is `FloorFootprint`** (`Turbo.Primitives/Rooms`): rotation swap, tiles,
  distance, adjacency. Two copies of the adjacency test forgot the east/west swap.

## Avatar effects
- A player's effects are `player_effects` rows, one per effect id, owned by `PlayerEffectGrain`
  (`IPlayerEffectGrain`). Write-through. Give effects through `GiveEffectAsync` and ask first with
  `CheckGiveEffectAsync` when something is charged for them; never write the table from anywhere
  else. The full rules, setup and the list of what is not built are in
  [Avatar effects](../avatar-effects.md).
- A running copy ends at an absolute `expires_at` (UTC, read through `TimeProvider`), never at a
  remaining time. The expiry timer only tells the player; every call also looks for ended copies.
- The room's avatar is the record of what is worn. The grain tells the presence which effect to
  wear and **every effect the player owns**; the room acts only where the avatar is bare or wears
  one of those, so an effect the hotel applied (riding, a game team, a freeze) is never
  overwritten. When one effect expires, pass only that id to take off.
- Ids the hotel applies itself (`EffectConfig.ReservedEffectIds`: game teams, a rider, freeze ids)
  can never be given as an inventory effect: the room cannot tell the two sources apart. Sums
  against the caps are made in a wide type, since a client names a purchase's quantity.
- The client reads `secondsLeftIfActive` of exactly `-1` as "not running" and zero is never sent
  for a running copy; `duration` is a divisor and is never zero.
- A catalog effect product keeps its effect id in the product's extra parameter
  (`EffectProducts`); the snapshot puts it where the client reads a sprite id. A purchase is
  validated before the buyer is charged.

## Pets and bots
- A pet or bot is a row (`pets`, `bots`) that is either in its owner's inventory
  (`room_id` null) or standing in a room. The hand-over is a grain call that moves the row
  first (`IInventoryGrain.TryCheckOut*Async` / `Return*Async`), so a crash in between leaves
  it where the database says it is. Rooms load theirs through `IRoomNpcProvider` and write
  stats back through `IRoomPersistenceGrain.EnqueueDirty*Async`; never update those rows from
  anywhere else.
- In a room they are avatars (`IRoomPet`, `IRoomBot`) with logic `default_pet` /
  `default_bot`; behaviour over time lives in `RoomPetTickSystem` / `RoomBotTickSystem`,
  actions in `RoomPetModule` / `RoomBotModule`. Pet commands arrive as chat
  ("&lt;name&gt; &lt;word&gt;") and are matched against `PetConfig.CommandWords`, which must agree
  with the hotel's `pet.command.<id>` texts.
- Pet-related furniture is a `[RoomObjectLogic]` like any other: `pet_food`, `pet_drink`,
  `pet_toy`, `pet_nest` (`IPetSupplyLogic`), `pet_breeding_nest`, `pet_package`, `pet_saddle`,
  `pet_revive`, `pet_rebreed`, `pet_fertilizer`, `pet_dye`, `pet_custom_part`, `monsterplant_seed`.
  The stock Habbo names are mapped to these by the data migration `MapPetFurnitureLogic`
  (`pet_rebreed` by `AddPetHasBred`); map new ones in a migration rather than editing rows by hand. A product used on a pet is a
  `UseWithPetInteraction` on the item; what an item applies (package contents, a hair part) is
  a JSON section read with `FurnitureExtraDataSections.Read` from the item's extra data, with
  the definition's extra data as the per-type default.
- A pet product can also be used straight from the inventory: Flash's use button sends
  `CustomizePetWithFurni` with the item's inventory ref, a floor item's id negated, so a
  negative id goes to `IRoomGrain.UsePetProductFromInventoryAsync`. The room gives the item its
  logic without placing it (`RoomObjectModule.EnsureLogic`) and asks the same
  `FurniturePetProductLogic.TryGetPetToTake` / `TryApplyAsync` as the room's own flow; it
  consumes the item from the inventory before applying it and gives it back when the product
  then has no effect. Only the room flow checks `IsOwnedOutrightBy`; the inventory flow refuses
  rented items (`FurnitureItemSnapshot.IsRented`) instead.
- A pet using something faces it and stays until it is done. Arriving beside an item it
  cannot stand on, it turns to the item's tile next to it (`RoomPetModule.TurnToItem`, through
  `FloorFootprint.NearestTileTo`); fed from a hand, it turns to the player (`TurnTo`). Every
  serving then holds the pet's next decision until its action ends (`StayUntilActionEnds`, in
  `FurniturePetSupplyLogic` for all its kinds). Only the drink held it, so a cat playing beside
  its ball looked round mid-play, and the turn faced the item's anchor, a diagonal from beside
  the far end of a long trough.
- Wire shapes (pet figure struct, `PetInfo` field order, bot skill ids, chatter string) come
  from the Flash client and are recorded on the enums and helpers under
  `Turbo.Primitives/Pets` and `Turbo.Primitives/Bots`; verify against the client before changing.

## Room runtime

- **Automatic human chat look-at keeps the body fixed and uses only its forward direction
  or either adjacent octant.** Ignore speakers outside that range and on the same tile.
  AS3's avatar renderer accepts the server's head direction; apply this rule in the chat
  system, not as a global clamp that changes explicit direction updates or pet behavior.
- **Where people come in is `RoomMapModule.EntryX` / `EntryY`, never the model's `DoorX` /
  `DoorY`.** It is the model's door, or the open tile nearest it when a model row puts its door
  off the map or on a hole. The spawn, the entry tile and camera the client is told
  (`RoomMapSnapshot.DoorX`), leaving on the door tile, the floor plan rebuild, the pets' search
  for a free tile and the wired place-furni refusal all read it; only the floor plan save reads
  the model's own door, to keep it. The spawn read the raw door and put players on a hole, or on
  0,0 for a door off the map, and each of the other five would have disagreed with it.
- **Rejected room entry does not load room contents.** Activation hydrates the metadata, rights
  and bans needed for the access decision. Load map, furniture and NPC state only after an
  allowed decision, before clearing the player's current room; recheck access after loading
  because bans, capacity or deletion may have changed while it awaited. Doorbell approval
  ensures contents are ready before marking the pending entry approved.
