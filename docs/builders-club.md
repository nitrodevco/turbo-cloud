# Builders Club and subscriptions

Implementation plan for the Builders Club, and for the subscription system it shares with Habbo
Club. Every phase of section 7 is built except the one item in 7.8, which is blocked on a
feature that does not exist; that entry says which and why. What was already in the tree before
this work is called out as it comes up, because a good deal of it was present as empty stubs.

Client behaviour below was read from the deobfuscated Flash client
(`com/sulake/habbo/**`), wire shapes from `packet-tool`, and the text and variable keys from the
hotel's `ExternalTexts.json` / `ExternalVariables.json`. Verify against those three before
changing anything here.

## 1. What Builders Club is

Subscription-gated furni lending. A second catalog tree (`CatalogType.BUILDER = "BUILDERS_CLUB"`)
whose offers a member *borrows* rather than buys: no price is drawn
(`HabboCatalogUtils.showPriceOnProduct` returns early for `BUILDERS_CLUB`), and placement goes
through its own packets instead of `PurchaseFromCatalog`. The borrowed furni is persisted and
stands in the room like any other, but nobody owns it: it can never enter an inventory, and
picking it up destroys it.

### Membership states

`HabboCatalog.refreshBuilderStatus` derives three states from `secondsLeft` and
`secondsLeftWithGrace`:

| State | Condition | Text key |
| --- | --- | --- |
| Full member | `secondsLeft > 0` | `builder.header.status.member` |
| Grace | `secondsLeft <= 0 < secondsLeftWithGrace` | `builder.header.status.grace` |
| Trial | both `<= 0` | `builder.header.status.trial` |

Crossing into grace or expiry fires `CATALOG_BUILDER_MEMBERSHIP_IN_GRACE` /
`CATALOG_BUILDER_MEMBERSHIP_EXPIRED`, which `HabboNotifications` turns into pop-ups. The client
counts the seconds down itself from `getTimer()`, so the server only pushes a fresh
`BuildersClubSubscriptionStatus` when something actually changes.

### Placement gates

`HabboCatalog.getBuilderFurniPlaceableStatusForOffer` returns a status code the client maps to a
`builder.placement_widget.error.*` text. The server enforces the same rules; the client's copy is
only there to grey out the drag handle.

| Code | Meaning |
| --- | --- |
| 1 | No offer |
| 2 | `furniCount < 0` or `furniCount >= furniLimit` — `limit_reached` |
| 3 | Not in a room |
| 4 | `roomControllerLevel < 3` — `not_room_owner` / `not_group_admin` |
| 5 | Guild room and `builders.club.furniture.placement.group.room.enabled` is false |
| 6 | Trial (`secondsLeft <= 0`) **and** another non-moderator user is in the room — `visitors` |

`furniCount` starts at `-1`, so the client refuses every placement until the server has answered
`BuildersClubQueryFurniCount` at least once. The count is **per player, hotel-wide**, across all
of that player's rooms — `builder.header.status.limit` reads
`Borrowed items: %COUNT%/%LIMIT%`.

### The trial bargain

A trial or grace member *may* place, but the server answers with `BuildersClubPlacementWarning`
instead of placing. `RoomMessageHandler.onBCPlacementWarning` shows `room.confirm.hide_room` —
*"You are using the Free Trial of Builders Club. Placing this furniture will hide the room from
the navigator"* — and on OK re-sends the identical place packet with a trailing `true`. The room
then goes invisible (`notification.invisible.room.bc`, `notification.builders_club.room_locked`,
`visit_denied_for_visitor`).

### A lapsed membership locks rooms

`notification.builders_club.membership_expired.message`: *"Your rooms that contain Builders Club
items will turn invisible if you want to continue building."* The `rooms.hidden_by_bc` column,
`RoomSnapshot.HiddenByBc` and the room-settings field all exist already and nothing sets them.

### Furni identity is an id band

`com.sulake.habbo.utils.FurniId`:

```text
normal          id <= 0x7FFB_FFFF            (2147401727)
temp (wired)    0x7FFC_0000 .. 0x7FFD_FFFF
builders club   0x7FFE_0000 .. 0x7FFF_FFFF
```

Five client sites read it, and this is the **only** way the client knows a furni is Builders
Club:

- `InfoStandFurniView.update` — green header, owner drawn as `${builder.catalog.title}`, and
  clicking the owner row opens the Builders Club catalog at that offer.
- `InfoStandWidgetHandler.pickupObjectWithConfirmation` — `room.confirm.not_in_warehouse` when
  the offer is no longer borrowable.
- `RoomEngine` (two sites) — suppresses the fly-to-inventory pickup animation.
- `ChooserItem.owner` — labels the furni chooser row `"Builders Club"`.

### Adjacent, already half-wired

`BCFloorPlanEditor` disables *save* unless `bcSecondsLeft > 0` or security level 4 and above, and
large floor plans need the `BUILDER_AT_WORK` perk, which the SSO handler already sends as
`IsAllowed = false`. Both are out of scope here; `UpdateFloorPropertiesMessageHandler` is an empty
stub today.

### What is hotel data, not code

`FurnitureData.json` carries `bc="1"` and `bcofferid` per furni; without them the client shows no
Builders Club button on the info stand and drops the furni from Builders Club search results.
`ExternalVariables.json` carries `builders.club.enabled`, `builders_club.try_page`,
`builders_club.buy_membership_page` and
`builders.club.furniture.placement.group.room.enabled`, which has to agree with the server's own
config.

## 2. Packets

Every packet below is now implemented. The **Was** column records what stood in the tree before
this work, because it is what explains the shape of the change: the headers, parsers and
serializer registrations were all there, and most of the gaps were empty records and handlers
that returned without doing anything.

### Builders Club

| Direction | Message | Was |
| --- | --- | --- |
| in | `BuildersClubQueryFurniCount` | Contract fine, handler is a no-op |
| in | `BuildersClubPlaceRoomItem` | **Missing the trailing `bool`** in both contract and parser |
| in | `BuildersClubPlaceWallItem` | Same |
| out | `BuildersClubSubscriptionStatus` | Contract fine; the SSO handler sends all zeros |
| out | `BuildersClubFurniCount` | **Contract and serializer missing entirely** (`int furniCount`) |
| out | `BuildersClubPlacementWarning` | Contract empty, serializer empty |

`BuildersClubPlacementWarning` wire shape: `typeCode:int` (0 floor, 1 wall), `pageId:int`,
`offerId:int`, `extraParam:string`, then `x,y,direction:int` for a floor item or
`wallLocation:string` for a wall item. `pageId` may be `-1` — the info stand's "place more"
button sends that — so echo back whatever arrived rather than resolving it.

### Habbo Club

| Direction | Message | Was |
| --- | --- | --- |
| out | `UserRights` | Contract fine; the SSO handler hardcodes `ClubLevelType.Vip` |
| in/out | `ScrGetUserInfo` / `ScrSendUserInfo` | Serializer complete, handler sends zeros |
| in/out | `GetClubOffers` / `HabboClubOffers` | Contract and serializer empty |
| in/out | `GetHabboClubExtendOffer` / `HabboClubExtendOffer` | Contract and serializer empty |
| in | `PurchaseBasicMembershipExtension` | Handler is a no-op |
| in | `PurchaseVipMembershipExtension` | Handler is a no-op |
| in/out | `GetClubGiftInfo` / `SelectClubGift` / `ClubGiftInfo` / `ClubGiftSelected` | Contracts empty, handlers no-ops |
| out | `ClubGiftNotification` | Contract empty |
| out | `NotificationDialog` | Contract and serializer empty |

## 3. Subscriptions, shared by both clubs

New table, one row per player and subscription type:

```text
player_subscriptions
  id, player_id, subscription_type, expires_at, first_subscribed_at,
  total_days_subscribed, periods_purchased, furni_limit
```

- `SubscriptionType { HabboClub = 0, BuildersClub = 1 }` in
  `Turbo.Primitives/Players/Enums/SubscriptionType.cs`.
- `PlayerSubscriptionSnapshot` in `Turbo.Primitives/Players/Snapshots/`, built from the row by an
  extension in `Turbo.Database/Extensions/PlayerEntityExtensions.cs`.
- `furni_limit` lives on the row because a Builders Club extension raises it
  (`builder.front_page.extend` is "Extend Limit", and
  `notification.builders_club.membership_extended.message` quotes the new limit).

### `IPlayerSubscriptionGrain`

In `Turbo.Players/Grains/`, keyed by player id, **write-through**, shaped like
`PlayerWalletGrain`. It owns:

- `GetAsync(SubscriptionType, ct)` — snapshot carrying `SecondsLeft`, `SecondsLeftWithGrace`,
  `FurniLimit` and `MaxFurniLimit`.
- `ExtendAsync(SubscriptionType, days, ct)` — adds days from `max(now, expires_at)`, bumps
  `periods_purchased` and `total_days_subscribed`, and raises `furni_limit` by the configured
  step, clamped to the configured maximum.
- `HasActiveAsync(SubscriptionType, ct)` — the cheap predicate for room entry and for the wired
  `@is_hc` variable, which `AGENTS.md` listed as reserved for want of this system (phase 6).

The grain sends its own outbound on every change, through `SendComposerToPlayerAsync`:
`UserRightsMessage`, `ScrSendUserInfoMessageComposer` and
`BuildersClubSubscriptionStatusMessageComposer`. No caller builds or sends those.

The `notification.builders_club.membership_extended` pop-up is not among them.
`NotificationDialogMessageComposer` is still an empty record with an empty serializer, and giving
it a payload is a notification-system change that every other pop-up in the hotel would use — it
is not a subscription concern. It lands with the rest of the Builders Club notifications in
phase 4.

### Purchasing

`CatalogPurchaseGrain.PurchaseOfferFromCatalogAsync` splits an offer's products:
`ProductType.HabboClub` (already defined, legacy `"h"`) goes to
`IPlayerSubscriptionGrain.ExtendAsync` with the day count from `catalog_products.extra_param`;
everything else stays on the existing inventory path in
`InventoryGrain.GrantCatalogOfferAsync`. A subscription is not inventory and must not be routed
through `InventoryGrain`.

`PurchaseBasicMembershipExtension` and `PurchaseVipMembershipExtension` are the same call with the
offer id the client was handed. `GetClubOffers` and `GetHabboClubExtendOffer` project the club
offers out of the normal catalog snapshot, with `months = days / DaysPerPeriod` and
`extraDays = days % DaysPerPeriod`.

### Config

A nested `SubscriptionConfig` on `PlayerConfig`, mirroring how `CatalogConfig` holds
`LtdRaffleWeightConfig`: `GraceDays`, `DaysPerPeriod` (31), `BuildersClubBaseFurniLimit`,
`BuildersClubFurniLimitPerExtension`, `BuildersClubMaxFurniLimit`.

It belongs to the player module and not to the catalog for two reasons: `Turbo.Catalog` already
references `Turbo.Players` and not the other way round, and `AGENTS.md` forbids passing a limit
into a grain as an argument — the grain that enforces the limit has to read it from its own
module's config.

### Expiry while offline

A lapsed membership has to lock the player's rooms whether or not they are online, so expiry
cannot ride on a per-player timer that only runs while the grain is activated. It is swept by the
hotel-wide grain in section 5.

## 4. The Builders Club catalog

- Add `catalog_type` (the existing `CatalogType` enum) to `catalog_pages`. Offers and products
  inherit their type from their page.
- `CatalogSnapshotProvider` filters pages by `CatalogType` before building the snapshot. Note that
  `RootPageId = pages.First(x => x.ParentEntityId == null)` stops being correct the moment there
  are two trees, so the filter has to come first.
- Register `ICatalogSnapshotProvider<BuildersClubCatalog>` in `CatalogModule` and drop the
  `CatalogTypeNotSupportedException` branch from `CatalogService.GetCatalogSnapshot`.
- `GetCatalogIndexMessageHandler` and `GetCatalogPageMessageHandler` already dispatch on
  `message.CatalogType`; neither needs a change.
- Builders Club offers carry a zero price. The client never draws one for this catalog type.

## 5. Builders Club furni

### 5.1 Identity and storage

Builders Club furni cannot be rows in `furniture`. Its ids have to fall inside
`0x7FFE_0000 .. 0x7FFF_FFFF` or the client renders it as ordinary owned furni, and inserting an
explicit id that high would drag InnoDB's auto-increment counter up with it and poison every
normal furni id after. It also does not belong there: `furniture` means "furni somebody owns", and
every inventory, trade and ownership-count query in the tree reads it that way.

So, its own table, with the room-facing id as part of the key:

```text
builders_club_furniture
  room_id, room_object_id          PRIMARY KEY (room_id, room_object_id)
  definition_id, placed_by_player_id, offer_id
  x, y, z, direction, wall_offset, extra_data
```

**Ids are allocated per room, inside the band.** A furni object id only has to be unique within a
room — the client addresses room objects per room session, and every server-side consumer of one
(the map, the tile stacks, wired, the move and pickup handlers, `RoomPersistenceGrain`) is already
scoped to a single room. So each room allocates the lowest free id in the band among the rows it
has loaded, which is a `HashSet` scan over a handful of items. There is no hotel-wide pool, no
allocator grain on the placement path, and no ceiling: the live count is bounded by the room's
furni cap, not by how many items the room has placed over its lifetime.

**The id is stored, not re-derived at load.** Wired furni variable values persist with the room and
are keyed by furni id. If ids were handed out fresh on each load, reload order would shift them
and one Builders Club furni would inherit another's stored values. That is the failure
`RoomWiredSystem.ForgetStoredValuesOfTemporaryFurni` exists to prevent for temporary furni, which
only escapes it because temporary furni is gone when the room unloads. Builders Club furni is not.

Because the stored id *is* the room-facing id, there is no mapping layer anywhere: no surrogate
row id, no translation in the loader, the handlers or the persistence flush. `RoomPersistenceGrain`
is already keyed by room, so its updates and deletes are
`WHERE room_id = @room AND room_object_id IN (...)`.

Recycling a freed id within a room is safe only if the id carries nothing forward, so
`ForgetStoredValuesOfTemporaryFurni` is generalised from its `objectId.Value >= 0` guard to
"temporary **or** builders club". That is correct on its own terms — a picked-up Builders Club
furni is destroyed, and its wired values should go with it.

A `FurniIdBands` static class under `Turbo.Primitives/Furniture/` mirrors the client's `FurniId`,
per the rule in `AGENTS.md` about tables of protocol values the client interprets. It declares
only the Builders Club band, because that is the only one of the three this server keeps to; the
layout of all three is on the class, where it explains why the band starts where it does.

> Noted but out of scope: `RoomItem.IsTemporary => ObjectId.Value < 0` puts wired temporary furni
> in the client's *normal* band, so its `"Temp (Wired)"` label and colour never show. Moving it
> into `0x7FFC_0000 .. 0x7FFD_FFFF` would fix that but would give up the "every handler already
> refuses non-positive ids" safety property `AGENTS.md` documents. Decide that separately.

### 5.2 `IBuildersClubGrain`

One hotel-wide singleton in `Turbo.Catalog/Grains/`, beside `CatalogLtdRaffleGrain`, marked
`[KeepAlive]` and shaped like `BadgeDirectoryGrain`: it answers from memory, runs one query on
activation, and recounts on a timer.

It owns exactly two things, both of which are genuinely hotel-wide because a player's Builders
Club furni is spread across their rooms:

- **Borrowed counts per player.** One grouped query on activation, adjusted on each placement and
  pickup. It answers `BuildersClubQueryFurniCount`, backs the `furniLimit` check, and sends
  `BuildersClubFurniCountMessageComposer` itself after every change.
- **The lapse sweep.** On a `RegisterGrainTimer` (static callback form), it works out which rooms
  holding borrowed furni have a borrower whose membership has run out past its grace period, and
  sets `hidden_by_bc` on them; the room sends `notification.builders_club.room_locked` itself. A
  renewal clears the flag, and reaches the club at once rather than at the next sweep
  (`OnSubscriptionChangedAsync`). Only the rooms whose state actually differs are touched, so a
  hotel where nothing changed activates no room at all. The door mode is never written; see
  section 6.

Calls to rooms go out with `LogAndForget`. The room never awaits this grain outside the placement
path, and this grain never awaits a room, so the call graph stays acyclic.

### 5.3 Placement

Handlers stay orchestration-only. `BuildersClubPlaceRoomItemMessageHandler` parses and calls
`IRoomService.PlaceBuildersClubFloorItemInRoomAsync`, which is where every other furni-placing
packet goes; the service finds the room grain and logs a failure. The wall twin passes the
location string on unparsed, because the confirmation the client may be asked for has to echo
back exactly what it sent; the grain parses it with `WallPosition.TryParse`.

The work lives in a new partial `Turbo.Rooms/Grains/Modules/RoomFurniModule.BuildersClub.cs`,
sibling to `RoomFurniModule.Temporary.cs`, in this order:

1. `GetControllerLevelAsync >= Owner`, or `GroupAdmin` when the group-room option is on. This is
   the client's codes 4 and 5.
2. `IPlayerSubscriptionGrain.GetAsync(BuildersClub)` and
   `IBuildersClubGrain.GetBorrowedCountAsync`; refuse over the limit. Code 2.
3. Resolve the offer from `ICatalogService.GetCatalogSnapshot(CatalogType.BuildersClub)`. Take the
   offer id from the client and nothing else — never a definition id — and reject an offer that is
   not in the Builders Club tree.
4. Trial and grace gate. If `SecondsLeft <= 0` and any other player is in the room, refuse
   (code 6). If `SecondsLeft <= 0`, the room is not already `HiddenByBc`, and `confirmedHideRoom`
   is false, send `BuildersClubPlacementWarning` echoing the request and stop. Nothing is hidden
   here — only permission is settled — so a placement that then fails on a blocked tile does not
   take the room off the navigator for nothing.
5. Ordinary map validation through `ValidateNewFloorItemPlacementAsync`.
6. Pick the lowest free band id among the room's loaded Builders Club items, attach and announce
   through the existing `PlaceFloorItemAsync` / `PlaceWallItemAsync`, then await
   `IRoomPersistenceGrain.InsertBuildersClubItemAsync`. The item is placed before the row is
   written, unlike `RoomFurniModule.CreateWallItemAsync`, because a floor item's height is not
   known until the map has placed it; a write that fails takes the furni back out again. The
   insert lives in the persistence grain so the room's turn stays free of I/O.
7. Count the borrow, and for a lapsed member hide the room now that there is something in it to
   hide it for.

### 5.4 Ownership, persistence and pickup

- `IRoomItem` gains `Ownership` (`FurnitureOwnershipType`, already defined with a `BuildersClub`
  member and a comment saying nothing grants it yet), derived from the id band.
  `FurnitureTypeVariable` reads it instead of its current
  `IsTemporary ? Temporary : Normal`, which closes that gap.
- `RoomItemsProvider.LoadByRoomIdAsync` loads both tables for the room and merges the results.
- `RoomPersistenceGrain`: `FlushDirtyItemsAsync` and `FlushDeletedItemsAsync` split each batch on
  `FurniIdBands.IsBuildersClub` and write to the right table. Builders Club furni has no "removed
  from the room but kept" state, so a Builders Club id in `RemovedItemIds` means **delete**.
- `RoomActionModule.RemoveItemByIdAsync` and `ReturnItemsToOwnersAsync` take the
  `IsTemporary`-style branch for Builders Club furni — it never reaches an inventory — but unlike
  temporary furni it does enqueue a row delete and decrement the player's borrowed count. Every
  site that currently checks `IsTemporary` needs the Builders Club case too, including room
  deletion and an ended rentable space.
- Pickup rights keep the existing `GetFurniPickupTypeAsync` rules **minus** the "your own furni is
  always yours" fallback: nobody owns a Builders Club item.
- Moving and rotating already work. The ids are positive, so they pass every handler guard, and the
  move path is id-agnostic once persistence routes on the band.

## 6. Room locking

`HiddenByBc` already reaches `RoomEntity`, `RoomSnapshot` and `RoomSettingsData`. Two things still
need to read it:

- `RoomEntryModule.CheckAccessAsync` denies non-owners when `HiddenByBc` is set, through the new
  `RoomEntryAccessType.HiddenByBuildersClub`, and the refusal carries
  `notification.builders_club.visit_denied_for_visitor`. The owner's half of that pair is
  deliberately not sent; see `BuildersClubNotifications.VISIT_DENIED_FOR_OWNER`.
- `NavigatorService.IsPublic` checks `HiddenByBc` beside `DoorMode.Invisible`, and the door mode
  is **not** touched when a room is hidden. The flag alone decides, so the owner's own choice of
  door survives and there is nothing to put back on renewal — which is why no column records a
  previous door mode.
- `HiddenByBc` lives on `RoomInfoSnapshot` rather than `RoomSnapshot`, because the navigator
  filters on it without loading the room. A change to it invalidates every cached listing the
  room could appear in, or a cached row would go on claiming the room is public.

## 7. Build order

1. **Subscriptions.** *Done.* Table, enum, snapshot, `PlayerSubscriptionGrain`, config. Real
   `UserRights` and `ScrSendUserInfo`. Habbo Club purchase routing in `CatalogPurchaseGrain`.
   `HabboClubOffers`, `HabboClubExtendOffer` and the two membership-extension handlers.
   `SeedHabboClubOffers` seeds three memberships on a hidden page so a fresh hotel has something
   to buy.
2. **The Builders Club catalog.** *Done.* `catalog_type` column, provider filter, `CatalogService`
   branch, module registration, and a seeded Builders Club root page — a catalog with no root
   cannot be sent, and the client asks for it again on every furni it selects. Both the index
   handler and its serializer now treat an empty tree as nothing to send rather than a crash.
3. **Builders Club furni.** *Done.* `builders_club_furniture` table, `FurniIdBands`,
   `BuildersClubGrain`, `RoomFurniModule.BuildersClub`, the loader, persistence and pickup
   branches, the three packet handlers, the missing `BuildersClubFurniCount` contract and
   serializer, the `BuildersClubPlacementWarning` contract and serializer, and `Ownership` on
   `IRoomItem`. A lapsed member's room is hidden here, since that is the price of the borrow;
   what phase 4 adds is hiding a room whose owner lapsed without placing anything.
4. **Lapse handling.** *Done.* The sweep timer in `BuildersClubGrain.Lapse`, room hiding, entry
   denial, and the notifications — which meant giving `NotificationDialogMessageComposer` its
   payload, so the hotel now has a working notification packet for everything else too.
5. **Club gifts.** *Done.* `ClubGiftInfo`, `SelectClubGift` and `ClubGiftNotification`, with the
   `player_club_gifts` ledger and `catalog_offers.club_gift_days_required` behind them. A member
   earns one gift per `SubscriptionConfig.ClubGiftIntervalDays` of membership used up, and
   claiming is serialised through `CatalogPurchaseGrain` so two clicks cannot spend one gift
   twice. Gifts are never marked VIP: the client measures a VIP gift against the player's VIP
   days alone, and this hotel keeps no separate VIP tally, so such a gift would read as for ever
   out of reach.
6. **The wired `@is_hc` variable.** *Done.* The avatar carries when a membership runs out
   (`IRoomPlayer.HabboClubExpiresAt`), loaded on entry beside the badges and pushed again on a
   purchase (subscription grain → presence → room); the variable compares it against now. It
   holds the moment rather than a verdict so that a membership expiring while its owner stands
   in the room needs no push and no timer. It sits at user `Base 40`, between
   `@achievement_score` and `@has_rights`, leaving 60 and 20 reserved for `@level` and
   `@is_group_admin`.
7. **The floor plan editor.** *Done.* `UpdateFloorProperties` was an empty handler, so the gate
   had nothing to gate; the save itself is built now, in `RoomMapModule.FloorPlan`. A room that
   draws its own plan gets a `room_models` row of its own, marked `custom` so it is never offered
   to somebody creating a room, and the second save edits that row — writing through to the
   shared model would redraw every other room built on it. Saving needs an active Builders Club
   membership, which is what `BCFloorPlanEditor` greys out its own save button on. Afterwards the
   room is put back together on the new plan and streamed to everybody standing in it, because
   nothing they were told about the old one still holds. No migration: `rooms.wall_height`,
   `thickness_wall`, `thickness_floor` and `room_models.custom` were all already there.
8. **Not built, and why.** One thing, waiting on something outside the Builders Club.
   - `BUILDER_AT_WORK` gates large floor plans, and its refusal text is
     `requirement.unfulfilled.group_membership`: in the real hotel it is a group perk, not a
     Builders Club one. Tying it to a Builders Club membership would invent a rule this client
     does not have, so the SSO handler still sends it as not allowed. It belongs to whoever
     builds groups. What it lifts is now real, though: the server enforces
     `RoomConfig.FloorPlanMaxArea` on every save, counted the way the editor counts it
     (`(width - 1) * (height - 1)`), so granting the perk is what would let a plan past it.

Gifting a membership is also not possible, because `PurchaseFromCatalogAsGift` is an empty
handler for every offer, not just these. The seeded club offers carry `can_gift = 0` so the
client does not draw a button that goes nowhere.

## Validation

```bash
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudFastCheck
dotnet build Turbo.Main/Turbo.Main.csproj -t:TurboCloudQualityGate
```
