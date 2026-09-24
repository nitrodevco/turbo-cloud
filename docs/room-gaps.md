# What the room system is still missing

A survey of the room system against the client, taken on 2026-09-20. It is here so the next
person does not have to re-derive it, and so a gap can be argued with rather than rediscovered.

## How this was measured

Three passes, because no one of them sees everything:

1. `python scripts/packetgap.py Room Roomsettings Navigator Userdefinedroomevents` — what
   nitro-next uses that this server does not answer. Its reach stops at what the port itself
   implements, and its `STUB` / `UNSENT` verdicts are heuristics.
2. The server's own room domains, read directly: handlers whose body is
   `await ValueTask.CompletedTask`, and composers still carrying
   `TODO: add properties if/when identified`.
3. The rules inside the room grain that are written but not enforced, found by reading
   `RoomSecurityModule` and its neighbours.

**Every line below was opened and read.** The report's heuristics are wrong often enough to
matter: it calls the six wired `Update*Composer` handlers stubs, when each is a one-line subclass
of `UpdateWiredMessageHandler<T>` that does the work in the base; and it calls
`WiredSaveSuccessEventMessageComposer` empty, when that packet genuinely carries no payload.
Neither is a gap.

## 1. Missing because the system behind them does not exist

Nothing here is a packet that wants writing. Each waits on a feature, and writing the packet
alone would leave a handler that lies.

### Guilds

Nothing in the hotel has groups, so `RoomGrain.GetIsGroupRoomAsync` returns a flat `false` and
`RoomSecurityModule.GetControllerLevelAsync` carries the branch commented out
(`// if has perm group_admin GroupAdmin`, `// check if belongs to group`). That one `false` is
load-bearing in more places than it looks:

- `GetGuildFurniContextMenuInfo` / `GuildFurniContextMenuInfoMessage` — the guild furni menu.
- `FavoriteMembershipUpdateMessage` — composer exists with no payload
  (`roomIndex, habboGroupId, status, habboGroupName`).
- `RoomSecurityModule.CanManipulateFurniAsync` has a `canGroupDecorate` local hardcoded to
  `false`, so a group room could never be decorated by its members even if groups existed.
- Builders Club placement in group rooms (`RoomConfig.BuildersClubInGroupRooms`) is written and
  unreachable for the same reason.
- The `BUILDER_AT_WORK` perk, which lifts the floor plan area limit, is a group perk in the real
  hotel. See `docs/builders-club.md` §7.8.

### The room queue and spectators

- `ChangeQueueComposer` — handler is a no-op.
- `RoomQueueStatusMessage` — composer exists with no payload (flat id, then a list of
  name/target pairs).
- `YouAreNotSpectatorMessage` — composer and serializer are both complete, carrying the flat id.
  Nothing constructs it, because nothing makes anyone a spectator in the first place.

A full room turns visitors away today (`RoomEntryAccessType.Full`). A queue would hold them
instead, and spectating would let them watch without a slot.

### Purchasable clothing

`CustomizeAvatarWithFurniComposer` — handler is a no-op. The `clothing_change` furni logic
exists, but there is no clothing inventory for a booth to hand anything out of. The two
`UserPurchasableChatStyle*` messages have no composer at all and belong to the same missing
system (things a player owns and may wear or speak in).

### Rented furni

`GetRentOrBuyoutOffer`, `ExtendRentOrBuyoutFurni` and `ExtendRentOrBuyoutStripItem` are all
no-op handlers. The info stand already draws the rent and buyout buttons from
`FurnitureData`, so the client asks; nothing answers. Distinct from rentable *spaces*, which are
built.

### NFT

`UserNftChatStylesMessage` is never sent. Out of scope for a private hotel unless somebody wants
the collectibles system, which is a no-op handler across the board.

## 2. Missing and buildable today

These need no new system, only the work.

| What | State | Shape |
| --- | --- | --- |
| `UseObjectMessage` | never sent | `userId:int, itemType:int` — tells the room somebody used a hand item, so the client animates it |
| `SpecialRoomEffectMessage` | never sent | `effectId:int` — the snowstorm-style room-wide effect |
| `BotSkillListUpdateMessage` | never sent | bots exist and are complete otherwise; nothing on the server changes a bot's skills at runtime |
| `ConfigurationItemStatesMessage` | no composer | four booleans: hand item control blocked, chooser disabled, free furni movement, invisible furni. `RoomEngine.activeRoomHasFreeFurniMovementsMode` already reads the third |
| `ObjectRemoveConfirmMessage` | composer has no payload | `category, id, confirmTitle, confirmBody` — the "are you sure" before a pickup destroys something |
| `RoomMessageNotificationMessage` | composer has no payload | `roomId, roomName, messageCount` — the "your room has messages" bubble |

`ConfigurationItemStates` and `ObjectRemoveConfirm` are the two worth doing first: both are
small, both are about furni the room already owns, and both are drawn by the client the moment
they arrive.

## 3. Rules written but not enforced

Found by reading rather than by the tool. None of these is a packet.

- **No staff rank reaches a room.** `SecurityLevelType` exists and nothing sets it;
  `RoomPlayerAvatar.IsModerator` is `init` and always `false`; `SSOTicketMessageHandler` sends
  `SecurityLevelType.None` flat. So `// if has perm any_room_owner true` in
  `RoomSecurityModule.IsRoomOwner` cannot be honoured, moderators cannot enter a locked room, and
  the Builders Club trial rule ("nobody else in the room") counts staff as ordinary visitors.
  This is the single gap that most other room rules are waiting on.
- **`RoomSecurityModule.CanPlaceFurniAsync` carries a `// TODO placement rules?`** and simply
  defers to `CanManipulateFurniAsync`. Placing and moving are the same permission today.
- **Stealing furni is unimplemented** (`// if can steal furni, SendToRequester` in
  `GetFurniPickupTypeAsync`), so `FurniturePickupType.SendToRequester` is a member nothing
  returns.
- **`RoomAvatarModule.CreateAvatarFromPlayerAsync` has a `// TODO get a valid tile`**: a room
  whose door tile is out of bounds drops the avatar at `0,0` rather than looking for somewhere it
  can stand. Reachable now that the floor plan editor can move a door.
- **`GetOccupiedTiles` counts floor items only.** `RoomTileFlags.FurnitureOccupied` is set from
  the floor stack, so a tile whose only furniture is on its wall reads as free and the floor plan
  editor will let it be deleted. The save path handles it safely — the wall item falls outside
  the new plan and goes home to its owner — so this is a missing warning, not lost furni.

## 4. Wired trading, chests and contracts

The largest single block in the report, and already recorded as deliberate in `AGENTS.md`: 33 of
63 outgoing packets in the `Userdefinedroomevents` tree are chests, contracts, self-donation,
permanent user variables, room logs and the web API. They are one system, not thirty gaps, and
the wired boxes that would drive them (triggers 25 and 26, actions 45 to 48, conditions 45 and
46, addons 18 and 20) are unwritten for the same reason.

Two smaller ones sit outside that block and could be done on their own:

- `WiredClickUserComposer` / `WiredClickUserResponseMessage` — no handler, no composer.
- `WiredEnvironmentMessage` — never sent.

## What is *not* missing

Worth recording so nobody goes looking: the map and heightmap, furni placement, movement and
stacking, room settings and their moderation rules (`WhoCanMute` / `WhoCanKick` / `WhoCanBan` are
all read by `RoomModerationModule`), bans, mutes, the doorbell, ratings, room events, the
navigator and its cached listings, trading, pets, bots, entry logs, the floor plan editor, the
wired boxes themselves, and `GetOccupiedTiles`. The `Navigator` and `Roomsettings` domains report
no gaps at all.
