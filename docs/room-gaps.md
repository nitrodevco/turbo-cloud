# What the room system is still missing

- [Introduction](#introduction)
- [How this was measured](#how-this-was-measured)
- [1. Missing because the system behind them does not exist](#1-missing-because-the-system-behind-them-does-not-exist)
    - [The room queue and spectators](#the-room-queue-and-spectators)
    - [Hoppers, VIP gates and talent shows](#hoppers-vip-gates-and-talent-shows)
    - [Chat styles granted in play](#chat-styles-granted-in-play)
    - [Rented furni](#rented-furni)
    - [Games](#games)
    - [NFT](#nft)
- [2. Complete on the wire, but nothing known causes them](#2-complete-on-the-wire-but-nothing-known-causes-them)
- [3. Missing and needing a decision on what it means](#3-missing-and-needing-a-decision-on-what-it-means)
- [4. Rules written but not enforced](#4-rules-written-but-not-enforced)
- [5. Wired](#5-wired)
- [Closed since the first survey](#closed-since-the-first-survey)
- [What is *not* missing](#what-is-not-missing)

## Introduction

A survey of the room system against the client, last taken on 2026-11-22 (first taken
2026-09-20, then 2026-10-11). It is here so the next person does not have to re-derive it, and so
a gap can be argued with rather than rediscovered.

## How this was measured

Three passes, because no one of them sees everything:

1. `python scripts/packetgap.py Room Roomsettings Navigator Userdefinedroomevents` — what
   nitro-next uses that this server does not answer, and `python scripts/wiredgap.py` — the wired
   codes the client has an editor for that no box returns. The packet report only reaches what
   the port itself implements, and its `STUB` / `UNSENT` / `MISSING` verdicts are heuristics.
2. The server's own room domains, read directly: handlers whose body is
   `await ValueTask.CompletedTask`, and composers still carrying
   `TODO: add properties if/when identified`.
3. The rules inside the room grain that are written but not enforced, found by reading
   `RoomSecurityModule` and its neighbours.

**Every line below was opened and read.** The report's heuristics are wrong often enough to
matter. On this pass it still lists, and none of these is a gap:

- the six wired `Update*Composer` handlers as stubs: each is a one-line subclass of
  `UpdateWiredMessageHandler<T>` that does the work in the base;
- the six `WiredLink*Composer` packets (create, rename, delete a network; get and save the
  configuration; get the connections) as missing: the revision parses all six into one
  `WiredLinkRequestMessage`, which `WiredLinkRequestMessageHandler` answers by operation;
- `SlideObjectBundle` (`RoomRollerSystem`), `FlatControllerRemoved` (`RoomSecurityModule`),
  `WiredTradeItemsUpdate` (`WiredTradeGrain`), `WiredEnvironment` (`RoomWiredSystem.ClickUser`),
  `FurnitureLabelsAndRegions` (`RoomWiredSystem.Labels`, sent with the room entry) and the two
  `VariableLeaderboard*` messages (`RoomVariableLeaderboardSystem`) as unsent: each is built in
  a grain or system the report does not follow.

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

### Hoppers, VIP gates and talent shows

`CustomUserNotificationMessage` is complete (`CustomUserNotificationType`) and never sent. The
client opens a window with it when a costume hopper, a VIP hopper or a VIP gate turns a user
away, or a talent show vote finds no stage or no audience. None of those furni has a logic on
the server, and there are no talent shows.

### Chat styles granted in play

A player's chat styles are rows, read when the player's settings grain loads; the purchasable
and NFT ones go out on every room entry (see *Closed* below). Nothing grants one during play,
so `UserPurchasableChatStyleChangedMessage`, which tells the client that one was added or taken
away (`added:bool, styleId:int`), has no composer.

### Rented furni

`GetRentOrBuyoutOffer`, `ExtendRentOrBuyoutFurni` and `ExtendRentOrBuyoutStripItem` are all
no-op handlers, and `FurniRentOrBuyoutOfferMessage` is never sent. The info stand already draws
the rent and buyout buttons from `FurnitureData`, so the client asks; nothing answers. Distinct
from rentable *spaces*, which are built.

### Games

`GamePlayerValueMessage` (a player's score in a game room) is never sent: there are no game
rooms.

### NFT

The collectibles hub's read requests are answered as Habbo answers an account with no wallets,
and a player's NFT chat styles go out on every entry. Minting, claiming a bonus or reward item,
transferring assets and buying mint tokens (`MintItem`, `NftCollectiblesClaimBonusItem`,
`NftCollectiblesClaimRewardItem`, `NftTransferAssets`, `PurchaseMintToken`) are no-op handlers.
Out of scope for a private hotel unless somebody wants a collectibles system.

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

## 4. Rules written but not enforced

Found by reading rather than by the tool. None of these is a packet.

- **`GetOccupiedTilesAsync` counts floor items only.** `RoomTileFlags.FurnitureOccupied` is set
  from the floor stack, so a tile whose only furniture is on its wall reads as free and the floor
  plan editor will let it be deleted. The save path handles it safely — the wall item falls
  outside the new plan and goes home to its owner — so this is a missing warning, not lost furni.
- **Free furni movement** (the third flag of `ConfigurationItemStates`) always goes out off. No
  configuration furni switches it; the only candidate, `free_placement_room`, is an internal
  "extras" item whose purpose is unproven.

## 5. Wired

`wiredgap.py` finds a box for every code the client has an editor for: triggers, actions,
conditions, selectors, addons and variable boxes alike, and no code claimed by two boxes.

Still missing around the wired menu and the trading block is self-donation:
`SelfDonateItemComposer` has no message on the server and `SelfDonationResultMessage` no
composer.

Chests, contracts and the transaction wired are done ([Wired Rules](rules/wired.md#wired-chests)).

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
- **Placing is a rule of its own.** `RoomSecurityModule.CanPlaceFurniAsync` lets a rights holder
  place furni only when `RoomConfig.RightsHoldersCanPlaceFurni` says so; it no longer just
  defers to moving.
- **A door that is no tile of its model.** The floor plan editor refuses one, but a model row
  written by hand or brought in by the import can put its door off the map or on a hole. People
  then come in on the open tile nearest it (`RoomMapModule.EntryX` / `EntryY`), and the client
  is told that tile as the entry tile; they used to land on the hole, or on `0,0`.
- **Purchasable clothing.** `CustomizeAvatarWithFurni` binds a clothing furni's figure sets to
  its owner (`FurniturePurchasableClothingLogic`, a `BindClothingInteraction`) and uses the furni
  up.
- **Chat styles on entry.** Every entry opens with the player's NFT chat styles (an empty list for
  an account without any), and an owner of purchasable styles is sent those too
  (`UserPurchasableChatStyles`), both from `RoomService`.
- **Habbicons** exist (`PlayerHabbiconGrain`); the room shows one through
  `RoomAvatarModule.ShowHabbiconAsync` (`RoomUseHabbicon`).
- **The wired menu.** User permanent variables are read and set
  (`WiredGetUserPermanentVariables`, `WiredSetUserPermanentVariable`), `WiredUpdateRoom` reloads
  the room's wired, the web API key is made (`WiredGenerateWebApiKey` /
  `WiredWebApiKeyResult`), and `WiredMenuError` is sent when the variables asked for belong to
  something no longer there.
- **Every wired code has a box**, among them give effect (52), override height (53), the reward
  track actions (58, 59), the level condition (44), the achievement enabler and action, the
  global placeholder (2000), the web API addon (2002) and the chest item type scanner (addon 18).
- **Wired link networks** are answered (see *How this was measured*).
- **`SpecialRoomEffect`** is the staff room command `:roomeffect rotate|shake|flip|disco`
  (`command.roomeffect`). The effect is the client's: a few seconds of play, nothing kept. It is
  not a snowstorm, as this page once said.

## What is *not* missing

The map and heightmap, furni placement, movement and stacking, room settings and their
moderation rules (`WhoCanMute` / `WhoCanKick` / `WhoCanBan` are all read by
`RoomModerationModule`), bans, mutes, the doorbell, ratings, room events, the navigator and its
cached listings, trading, pets, bots, entry logs, the floor plan editor, the wired boxes
themselves, wired link networks, rollers, and the configuration furni above. The `Navigator`
domain reports no gaps.
