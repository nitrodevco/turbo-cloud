# What the room system is still missing

A survey of the room system against the client, last taken on 2026-10-11 (first taken
2026-09-20). It is here so the next person does not have to re-derive it, and so a gap can be
argued with rather than rediscovered.

## How this was measured

Three passes, because no one of them sees everything:

1. `python scripts/packetgap.py Room Roomsettings Navigator Userdefinedroomevents` — what
   nitro-next uses that this server does not answer, and `python scripts/wiredgap.py` — the wired
   codes the client has an editor for that no box returns. The packet report only reaches what
   the port itself implements, and its `STUB` / `UNSENT` verdicts are heuristics.
2. The server's own room domains, read directly: handlers whose body is
   `await ValueTask.CompletedTask`, and composers still carrying
   `TODO: add properties if/when identified`.
3. The rules inside the room grain that are written but not enforced, found by reading
   `RoomSecurityModule` and its neighbours.

**Every line below was opened and read.** The report's heuristics are wrong often enough to
matter. On this pass it still lists, and none of these is a gap:

- the six wired `Update*Composer` handlers as stubs: each is a one-line subclass of
  `UpdateWiredMessageHandler<T>` that does the work in the base;
- `SlideObjectBundle` (`RoomRollerSystem`), `FlatControllerRemoved` (`RoomSecurityModule`),
  `WiredTradeItemsUpdate` (`WiredTradeGrain`) and `WiredEnvironment` (`RoomWiredSystem.ClickUser`)
  as unsent: each is built in a grain or system the report does not follow;
- `WiredSaveSuccessEventMessageComposer` as empty: that packet genuinely carries no payload.

## 1. Missing because the system behind them does not exist

Nothing here is a packet that wants writing. Each waits on a feature, and writing the packet
alone would leave a handler that lies.

### The room queue and spectators

- `ChangeQueueComposer` — handler is a no-op.
- `RoomQueueStatusMessage` — composer exists with no payload (flat id, then a list of
  name/target pairs).
- `YouAreSpectatorMessage` / `YouAreNotSpectatorMessage` — composers complete, never sent,
  because nothing makes anyone a spectator.

A full room turns visitors away today (`RoomEntryAccessType.Full`). A queue would hold them
instead, and spectating would let them watch without a slot.

### Purchasable clothing and chat styles

`CustomizeAvatarWithFurniComposer` — handler is a no-op. The `clothing_change` furni logic
exists, but there is no clothing inventory for a booth to hand anything out of. The two
`UserPurchasableChatStyle*` messages have no composer at all and belong to the same missing
system (things a player owns and may wear or speak in).

### Rented furni

`GetRentOrBuyoutOffer`, `ExtendRentOrBuyoutFurni` and `ExtendRentOrBuyoutStripItem` are all
no-op handlers, and `FurniRentOrBuyoutOfferMessage` is never sent. The info stand already draws
the rent and buyout buttons from `FurnitureData`, so the client asks; nothing answers. Distinct
from rentable *spaces*, which are built.

### Games

`GamePlayerValueMessage` (a player's score in a game room) is never sent: there are no game
rooms. `RoomUseHabbiconMessage` belongs to Habbicons, which do not exist either.

### NFT

`UserNftChatStylesMessage` is never sent. Out of scope for a private hotel unless somebody wants
the collectibles system, which is a no-op handler across the board.

## 2. Complete on the wire, but nothing known causes them

Each of these has a composer and serializer that match the client byte for byte. What is
missing is a reason to send one: the client never asks for any of them, and no text, furni or
wired box points at what the real server sends them for. Wiring one to an invented trigger would
be a guess, so each waits for a decision.

| What | Shape | What the client does |
| --- | --- | --- |
| `UseObjectMessage` | `roomIndex:int, flag:int` | Plays the avatar's "use" animation of whatever it carries while the flag is above 0 (`AvatarVisualization`). The client already loops that animation itself for drinks (hand items 999 and below), so it would only change things for hand items of 1000 and up. |
| `BotSkillListUpdateMessage` | `botId:int`, then `{id:int, data:string}` pairs | Replaces the bot menu's whole skill list. Only skills 7 (link), 8 (NUX step) and 14 (navigator search) read their data, so it belongs to hotel-run guide bots. A player's bot keeps `BotConfig.Skills` for life and never needs it. |
| `ShowEnforceRoomCategoryDialogMessage` | — | Asks the owner to pick a room category. Nothing on the server forces a category. |

## 3. Missing and needing a decision on what it means

- `RoomMessageNotificationMessage` — composer has no payload. The client reads
  `roomId:int, roomName:string, messageCount:int` and shows a bubble from
  `notifications.text.room.messages.posted`, a text the hotel's `ExternalTexts.json` does not
  have, so with today's texts it draws nothing at all. The packet sits beside the post-it
  messages, which suggests "N notes were left in your room", but nothing confirms it.
- `SpecialSystemChatMessage` — no composer; nitro-next listens for it.
- `WiredMenuErrorMessage` — composer has no payload, never sent.

## 4. Rules written but not enforced

Found by reading rather than by the tool. None of these is a packet.

- **`RoomSecurityModule.CanPlaceFurniAsync` carries a `// TODO placement rules?`** and simply
  defers to `CanManipulateFurniAsync`. Placing and moving are the same permission today.
- **`RoomAvatarModule.CreateAvatarFromPlayerAsync` has a `// TODO get a valid tile`**: a room
  whose door tile is out of bounds drops the avatar at `0,0` rather than looking for somewhere it
  can stand. Reachable now that the floor plan editor can move a door.
- **`GetOccupiedTilesAsync` counts floor items only.** `RoomTileFlags.FurnitureOccupied` is set
  from the floor stack, so a tile whose only furniture is on its wall reads as free and the floor
  plan editor will let it be deleted. The save path handles it safely — the wall item falls
  outside the new plan and goes home to its owner — so this is a missing warning, not lost furni.
- **Free furni movement** (the third flag of `ConfigurationItemStates`) always goes out off. No
  configuration furni switches it; the only candidate, `free_placement_room`, is an internal
  "extras" item whose purpose is unproven.

## 5. Wired

`wiredgap.py` lists eight codes without a box: give effect (52), override height (53), the two
reward track actions (58, 59), the level condition (44), the global placeholder (2000), the web
API addon (2002) and variable box 8. Of these only the reward tracks and the web API have furni
in the hotel; the others cannot be placed and are not gaps until a furni carries them
(`AGENTS.md`, "Wired").

Still missing around the wired menu and the trading block:

- user permanent variables (`WiredGetUserPermanentVariables`, `WiredSetUserPermanentVariable`
  and their results), the variable owners page and `WiredUpdateRoom`;
- self-donation (`SelfDonateItem` / `SelfDonationResult`);
- the web API key (`WiredGenerateWebApiKey` / `WiredWebApiKeyResult`);
- the chest item type scanner (addon 18), which needs context variables that hold values.

Chests, contracts and the transaction wired are done (`AGENTS.md`, "Wired chests").

## Closed since the first survey

Recorded so nobody goes looking:

- **Guilds** exist (`docs/groups.md`): group rooms, group rights and decoration, the guild furni
  menu and `FavoriteMembershipUpdate` all work.
- **Staff rank reaches a room.** `RoomPlayerAvatar.IsModerator` comes from the player's
  permissions, so the Builders Club trial rule no longer counts staff as visitors, and stealing
  furni is the `FURNI_STEAL` permission (`FurniturePickupType.SendToCtx`).
- **`ConfigurationItemStates`** carries the Invisible Furni Controller, the Handitem blocker
  (`handitem_block`, which also refuses a hand item passed between players) and the `:chooser`
  disabler (`chooser_disable`), told to the room on every change and to each player walking in.
- **`ObjectRemoveConfirm`**: picking up a wired variable box asks first
  (`wiredfurni.variables.pickup.*`) and goes through only on the client's confirmed resend
  (`IFurnitureLogic.GetPickupConfirmation`).
- **`WiredClickUser`** and `WiredEnvironment` are answered and sent.
- **`SpecialRoomEffect`** is the staff room command `:roomeffect rotate|shake|flip|disco`
  (`command.roomeffect`). The effect is the client's: a few seconds of play, nothing kept. It is
  not a snowstorm, as this page once said.

## What is *not* missing

The map and heightmap, furni placement, movement and stacking, room settings and their
moderation rules (`WhoCanMute` / `WhoCanKick` / `WhoCanBan` are all read by
`RoomModerationModule`), bans, mutes, the doorbell, ratings, room events, the navigator and its
cached listings, trading, pets, bots, entry logs, the floor plan editor, the wired boxes
themselves, rollers, and the configuration furni above. The `Navigator` domain reports no gaps.
