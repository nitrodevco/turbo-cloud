# Asset movements

Core can record every furni, pet, bot and currency that changes **owner**, **place** or
**existence** into one table, `asset_movement_outbox`, for a plugin to consume (an audit ledger, a
rollback tool, dupe detection). Core says what moved, from where, to where, why and who did it;
what that means is the plugin's business.

**Status:** the record, the recorder, the table, the switch, the wallet, the inventory's
furni, trades, the marketplace and the room's placements, pickups and use-ups, and the
dispatcher that hands the rows to plugins and prunes them (below). The holder, borrowed furni,
pet, bot and onboarding paths are wired up one by one in later changes.

## The switch

`Turbo:Movements:Enabled` (default `false`) applies at once (it is a live setting, see
`docs/settings.md`). Off, nothing is recorded and an emit point costs one read of the setting.
On, each recorded change writes its rows in the same save as the change.

Turning it on or off writes a marker row, so a consumer can tell "nothing happened" from "nothing
was recorded":

| Cause | Cause ref | When |
| --- | --- | --- |
| `recording.started` | `server` | The server started with recording on. |
| `recording.started` | `setting` | Recording was turned on while the server ran. |
| `recording.stopped` | `setting` | Recording was turned off while the server ran. |
| `recording.stopped` | `server` | The server stopped while recording. A crash writes none: the next `recording.started` follows another `recording.started`. |

A marker moves nothing (asset id 0, amount 0, both places `None`). Recording starts only once its
`recording.started` row is written, so every movement of a recording has a higher id than the
marker that opens it. It stops as soon as the setting says so; a save that recorded its rows just
before may still commit after the `recording.stopped` row.

## The record

`AssetMovement` (`Turbo.Primitives/Movements`): one asset, one change.

| Field | Meaning |
| --- | --- |
| `OperationId`, `Seq` | One action (a trade, a purchase of five items) is one operation id (at most 160 characters) with one record per asset, `Seq` from 0. Consumers drop a repeated pair: a retried save writes its rows again, so there is no unique key. |
| `OccurredAtUtc` | When the action happened. A room writes its furni up to two seconds later, so the outbox's order is not the order things happened in: order one asset's movements by `(OccurredAtUtc, OperationId, Seq)`. |
| `Asset` | Kind (`Furni`, `BorrowedFurni`, `Pet`, `Bot`, `Currency`), id, definition id; a currency has id 0 and its `CurrencyKind`. |
| `From`, `To` | Owner (0: nobody), kind (`None`, `Inventory`, `Wallet`, `Room`, `Holder`, `MarketplaceOffer`, `ChestCredits`), the place's id, and a position in a room. `None` is "does not exist": `From` on a creation, `To` on a destruction. |
| `Amount` | A currency's amount (always positive); 1 for anything else. |
| `ActorPlayerId` | Who did it (0: the server); may own neither place. |
| `Cause`, `CauseRef` | A stable code (`catalog.purchase`, `room.pickup`; at most 48 characters) and what caused it by id (an offer, a trade, a chest transaction, a command's execution; at most 64). |
| `Snapshot` | On a creation or destruction of a furni only: its definition and extra data, what it would take to make it again. |

No names and no free text: names are resolved when shown, so a rename or an erasure never touches
the movement stream.

The row keeps one position, the room end's: where the asset was put, or else where it was taken
from. A snapshot without extra data is stored as an empty string, so it still reads as a snapshot.

## Recording a movement

```csharp
// recorder: IAssetMovementRecorder (Turbo.Database/Movements), a singleton.
if (recorder.IsRecording)
    recorder.Record(db, AssetMovement.For(cause, 0, now, asset, from, to));
await db.SaveChangesAsync(ct); // the movement commits or rolls back with the change
```

`Record` adds the row to the caller's `DbContext` and does no I/O of its own, the way
`IAchievementFactRecorder` does (`docs/achievements.md`). Unlike that recorder it **never throws**:
a movement that threw would fail the save of the change it describes, and the change matters more
than its record. An invalid movement (blank or too long ids and causes, a time that is not UTC or
lies in the future, a negative or temporary id, an item amount other than 1, a currency without
its kind, a movement that moves nothing, a marker cause) is logged at error level and dropped, and
the change still commits.

Moves to another tile of the same room are not movements, and temporary furni (negative ids, no
row) never are.

## Causes

A call site that moves an asset says why with a `MovementCause`: `MovementCause.New(cause, actor,
causeRef)` mints the operation id (a UUIDv7) of a new action. An action that moves assets in more
than one call (a voucher paying credits and another currency, a vault claim of several payouts)
gives each later call its own range of seqs with `cause.StartingAt(n)`, so no two of its movements
share an (operation, seq) pair. The codes core writes are the constants in `MovementCauses`.

## The wallet

Every `IPlayerWalletGrain` method that moves a balance takes the caller's `MovementCause`:
`TryDebitAsync`, both `CreditAsync`, `CreditRewardAsync` and `CreditAchievementAsync`. While
recording, each currency it moves is one movement (`Wallet` -> `None` for a debit, `None` ->
`Wallet` for a credit; asset id 0 and its `CurrencyKind`), written in the same transaction as the
balance. Every call is one transaction (`InTransactionAsync`) with a receipt in
`wallet_credit_receipts`: the plugin's reference for a referenced credit, the award key for an
achievement credit, and otherwise the cause's operation id and first seq (`#op:<seq>:<operation>`,
hashed when too long). The same call made again under the same cause, a retry, changes nothing:
a debit answers success, a credit true. A debit of several currencies is one operation, numbered
from the cause's first seq, and takes all of them or none. A debit the balance cannot cover, a
refused credit and a replayed credit write nothing, and a refused one keeps no receipt.

A refund (`GrainFactoryExtensions.RefundAsync`) is given the charge's cause and credits under
`cause.ForRefund()`: the same cause code and actor, an operation of its own whose id is the
charge's with `-refund` added, and the charge's operation id as its cause ref, so a debit and its
refund pair up. The i-th currency is refunded at the charge's i-th seq, so a refund given again for
the same charge pays nothing twice.

| Cause | Where |
| --- | --- |
| `catalog.purchase`, `catalog.gift`, `catalog.room_ad`, `catalog.targeted_offer`, `catalog.ltd_raffle`, `catalog.habbicon` | the price (cause ref: the offer, series or habbicon offer), and its refund |
| `catalog.voucher` | a voucher's credits and currency (cause ref: the voucher) |
| `camera.purchase`, `camera.publish` | a photo's price (cause ref: the photo) and its refund |
| `group.create` | a group's price and its refund |
| `vault.claim` | an earnings vault payout (cause ref: the claim) |
| `market.tokens`, `market.buy`, `market.redeem` | marketplace tokens, a purchase (cause ref: the offer) and its refund, a redeemed sale (cause ref: the offer) |
| `room.rent` | renting a space (cause ref: the furni) and its refund |
| `use.credit_furni`, `use.crackable` | a redeemed credit furni, a cracked crackable's credits (cause ref: the furni) |
| `wired.chest_upgrade`, `wired.chest_withdraw` | a chest's upgrade price and its refund, credits a chest pays out (actor 0; cause ref: the chest transaction) |
| `reward.track`, `reward.quest`, `reward.daily_task`, `reward.achievement` | reward track premium and prizes, quest, daily task and achievement rewards |
| `staff.give_currency` | `:give` (actor: the staff member; one operation per player) |

## The inventory

The furniture custody (`IFurnitureCustody`, `Turbo.Furniture/Custody`) is the only writer of who
holds an inventory's furni and where. Each of its transitions is one unit of work
(`InTransactionAsync`) with its movements while recording, one per furni, numbered from the
cause's first seq; the inventory grain keeps its list and the client in step after the commit.
Every `IInventoryGrain` method that makes, uses up, wraps, unwraps or hands over a furni takes the
caller's `MovementCause`.

| Transition | Movement | Called by |
| --- | --- | --- |
| `CreateInInventoryAsync` | `None` -> `Inventory`, with a snapshot | `GrantFurnitureAsync`, `GrantLtdFurnitureAsync`, `GrantCatalogOfferAsync` |
| `CreateTeleportPairsInInventoryAsync` | `None` -> `Inventory` per half, linked, with a snapshot | `GrantCatalogOfferAsync` (after the plain furni of the offer) |
| `CreatePresentInInventoryAsync` | the present `None` -> `Inventory`, then its content `None` -> `Holder` (the present) | `ReceivePresentAsync`, `ReceiveStaffPresentAsync` |
| `TakeOutOfPresentAsync` | `Holder` (whom it was wrapped for) -> `Inventory` (the opener) | `UnwrapPresentAsync` |
| `DestroyInInventoryAsync` | `Inventory` -> `None`, with a snapshot | `ConsumeFurnitureAsync` |
| `TransferBetweenInventoriesAsync` | `Inventory` -> `Inventory`, all or none | `TransferFurnitureAsync` |

A grant that follows a debit of the same action starts after the debit's seqs
(`cause.StartingAt(debits.Count)`): a purchase is one operation, its price then its furni.

| Cause | Where |
| --- | --- |
| `catalog.purchase`, `catalog.gift`, `catalog.targeted_offer`, `catalog.ltd_raffle`, `camera.purchase` | the furni or present bought (cause ref: the offer, series or photo), after the price |
| `catalog.club_gift`, `catalog.bonus_rare`, `catalog.voucher` | a club gift (the offer), a bonus rare (the campaign), a voucher's furni (after its credits and currency) |
| `use.decoration` | a room paper used up (cause ref: the room), and given back when the room could not take it |
| `use.crafting` | each ingredient used up, then the product (cause ref: the crafting table) |
| `use.crackable` | a cracked crackable's credits, then each furni prize (cause ref: the crackable) |
| `use.present` | a present's content coming out to the opener (cause ref: the present) |
| `room.give_back_used` | an item used up for a refused grant, given back (cause ref: the item) |
| `pet.breed`, `pet.harvest`, `pet.unsaddle` | a monster plant seed for each owner, a harvested seed, a saddle taken off |
| `wired.give_reward` | a wired reward's furni (actor 0; cause ref: the reward box) |
| `reward.track` | a reward track furni prize |
| `staff.give_item`, `staff.gift` | `:giveitem` and `:gift` (actor: the staff member; one operation per item or present) |
| `trade`, `market.*` | below |

## The room

A room buffers where its furni stand and their data in its persistence grain and writes them on
the flush (`RoomConfig.DirtyItemsTickMs`). Who holds a furni and in which room is not buffered: a
placement, a pickup and a use-up are written through the furniture custody when they happen, by
the room's persistence grain (`WritePlacementAsync`, `WritePickupAsync`, `WriteUseAsync`), so
they wait for a flush under way and nothing older of that room lands after them. A pickup drops
the room's queued update of the item, and the flush writes no owner or room. A write that fails
stays queued, ahead of the next flush's updates, and is written again with its movements.

| Transition | Movement | Called by |
| --- | --- | --- |
| `PlaceInRoomAsync` | `Inventory` -> `Room` (the room, where it was put) | a player's placement, floor or wall, and a crackable's prize or a present's content put down |
| `TakeOutOfRoomAsync` | `Room` (who held it, where it stood) -> `Inventory` (the snapshot's owner: the picker when they keep it), with the data it left with; one write for any number | a pickup, `ReturnItemsToOwnersAsync` |
| `DestroyInRoomAsync` | `Room` -> `None`, with a snapshot, in the use's own transaction with the pets it changed | `DeleteItemByIdAsync`, a breeding nest |

| Cause | Where |
| --- | --- |
| `room.place`, `room.pickup` | a placement and a pickup (actor: the player; `room.pickup` actor 0 when the room sends a furni back) |
| `room.pickall`, `room.ejectall` | `:pickall` and `:ejectall`, one operation for all the furni |
| `room.delete`, `room.floorplan`, `room.rent_end`, `group.furni_removed` | furni sent home when the room goes (cause ref: the room), is left off a new floor plan (the room), a rented space ends (the rentable furni), the room's group goes (the room) |
| `room.delete_item` | a furni the client deletes |
| `use.credit_furni`, `use.crackable`, `use.present`, `use.effect_box`, `use.clothing`, `use.pet_supply`, `use.pet_product`, `use.pet_package`, `use.monsterplant_seed`, `pet.breed` | the furni a use takes out of the room (cause ref: the furni; `pet.breed`: the nest) |

## Trades

A trade is one operation (`trade`, actor: the player whose confirm completed it): the initiator's
items to the partner from seq 0, then the partner's items to the initiator. When the second half
fails, the first is handed back under the same operation after both, with the cause ref
`compensation`, so a half trade shows as such.

## The marketplace

Every move names its offer:

| Cause | Movements |
| --- | --- |
| `market.list` | one operation per listing: each item `Inventory` -> `None` (with its snapshot), then, once the offer rows exist, each item `None` -> `MarketplaceOffer` (the offer's id) under the offer's id as cause ref. A listing that cannot be saved gives the items back under the listing's refund cause. |
| `market.buy` | the price (seq 0), then the item made again `None` -> `Inventory` for the buyer (seq 1), both under the offer's id. The item is a new row: the offer id and the snapshot link it to the listing. |
| `market.cancel` | an offer taken back (or expired) made again in the seller's inventory, under the offer's id. |
| `market.redeem` | one operation per sale: its revenue credited to the seller under the wallet reference `marketplace:<offer>`, then the sale marked redeemed. A credit that fails leaves the sale waiting for the next redeem; a redeem stopped between the two pays nothing twice (the reference is paid once). |

## Consuming movements

A plugin reads the outbox through an `IAssetMovementConsumer` (`Turbo.Primitives/Movements`),
registered the way extension packets are (`docs/plugin-packets.md`): take
`IAssetMovementConsumerRegistry` from the host services in `StartAsync`, register, and dispose the
handle in `StopAsync`. Disposing stops delivery and cancels a batch in hand; a hot reload
registers the new load's consumer, which starts again from its own cursor. One consumer per name
at a time.

```csharp
private IDisposable? _consumer;

public Task StartAsync(IServiceProvider services, CancellationToken ct)
{
    var registry = services.GetRequiredService<IHostServices>()
        .GetRequiredService<IAssetMovementConsumerRegistry>();

    _consumer = registry.Register(new LedgerConsumer(/* its own DbContext factory */));

    return Task.CompletedTask;
}

public Task StopAsync(CancellationToken ct)
{
    _consumer?.Dispose();

    return Task.CompletedTask;
}
```

The consumer answers `GetCursorAsync` with the outbox id it has handled up to (0 for none), and
`HandleAsync(batch)` writes what it makes of the batch **and** `batch.Cursor` (or the cursor it
has, when that is higher) in one transaction of its own. Delivery is at least once, so it drops a
movement whose `(OperationId, Seq)` it has already applied; its effect is then exactly once.

| A batch's | Meaning |
| --- | --- |
| `Movements` | Outbox rows in id order: `Id`, the `AssetMovement`, `WrittenAtUtc`. Marker rows (`recording.started`, `recording.stopped`) come as rows too. |
| `Gap` | Ids pruned before this consumer read them (first to last), or null. An id in the range may never have held a row: a rolled-back save still used its ids. |
| `Cursor` | What to store with the batch. |
| `IsLate` | Rows found below the cursor (see the settle window); `Cursor` is unchanged. |

The dispatcher is one grain for the hotel (`IAssetMovementDispatcherGrain`). Registering a
consumer wakes it; it then polls every `PollMs`, and a poll that finds no consumer stops the
timer and reads nothing, so a hotel without a consumer never reads the outbox. Each poll:

1. **Prunes** rows older than `OutboxRetentionHours`, oldest first, `BatchSize` at a time and at
   most once an hour (again at once after a full batch), whatever the consumers have read. The
   newest row it removes becomes an `outbox.pruned` marker and the previous marker goes: every id
   up to the marker is gone. A consumer whose cursor is below it is given the range as `Gap`;
   the marker itself is never delivered as a row. A plugin that is off longer than the retention
   therefore knows exactly what it missed. Recording on without any consumer keeps every row,
   since nothing polls; turn recording on for a consumer.
2. **Hands each consumer** the rows past its cursor, `BatchSize` at a time, in id order, stopping
   at the first row written less than `SettleMs` ago: ids are given at insert, not at commit, so
   a save still committing can hold a lower id than rows already visible. A full batch is followed
   by the next at once. A consumer that throws is logged, its cursor is asked again, and the same
   rows come on the next poll; the other consumers are not held up.
3. **Looks again for skipped ids.** An id the reading stepped over is either a rolled-back save
   (never a row) or one slower than `SettleMs`; for `ReReadWindowMs` its row is looked for and,
   once it appears, handed over as a late batch (`IsLate`).

Each dispatcher activation keeps the cursors in memory and asks a consumer again after a failure.
Orleans may briefly run two activations; both then hand over the same rows, and the consumer's
own cursor and `(OperationId, Seq)` check absorb it. Each silo has its own registry, filled by the
plugins it loads.

| Setting (`Turbo:Movements`) | Default | |
| --- | --- | --- |
| `PollMs` | 1000 | How often the outbox is read while a consumer is registered. |
| `BatchSize` | 500 | Rows per call; rows pruned per go; skipped-id ranges kept per consumer. |
| `OutboxRetentionHours` | 72 | How long a row is kept; 0 keeps every row. |
| `SettleMs` | 2000 | How old a row must be before it is delivered. |
| `ReReadWindowMs` | 300000 | How long skipped ids are looked for; 0 stops looking. |

These apply after a restart; `Enabled` alone is live.

## Decided for now

These follow the design's recommendations and wait on review:

- Delivery is a same-transaction outbox, not an event published after the commit: a record that can
  go missing silently is worse than none for an audit or a rollback.
- `HandleAsync` returns nothing: the batch carries the cursor to store, so the dispatcher and the
  consumer cannot disagree on it, and a late batch cannot move it back.
- Pruning leaves a marker in place of the newest row it removed instead of a table of its own, so
  a gap survives a restart without core storing anything per consumer.
- Primitives will take a required `MovementCause` (operation id, cause, cause ref, actor) rather
  than an ambient context, so a path that forgets one does not build.
- Badges, effects, clothing and subscriptions are out of scope; pets and bots are tracked for
  custody only.
- In-room moves are not recorded.
- A refund is an operation of its own whose cause ref is the charge's operation id, not the
  charge's operation: the two can never collide on (operation, seq).
- The furni a listing destroys and the offer that holds it are two movements of one operation,
  the second under the offer's id (written with the offer rows, so the id exists); the design
  lists the offer row (C7) with the holders, but the listing to sale lineage needs it here.
- A trade's actor is the player whose confirm completed it.
- A cause names the action's actor as the player who did it (the buyer, the earner); wired and
  other server-driven moves are actor 0. `:give` has no execution id to use as its cause ref yet.
