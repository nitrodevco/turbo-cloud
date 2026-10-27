# Asset movements

- [Introduction](#introduction)
- [The switch](#the-switch)
- [The record](#the-record)
- [Recording a movement](#recording-a-movement)
- [Causes](#causes)
- [Who writes the rows](#who-writes-the-rows)
- [The wallet](#the-wallet)
- [The inventory](#the-inventory)
- [The room](#the-room)
- [Holders, made-in-room and borrowed furni](#holders-made-in-room-and-borrowed-furni)
- [Pets and bots](#pets-and-bots)
- [Trades](#trades)
- [Gifts](#gifts)
- [The marketplace](#the-marketplace)
- [Consuming movements](#consuming-movements)
    - [The server's currency ledger](#the-servers-currency-ledger)
- [Reversing a movement](#reversing-a-movement)
- [The plugin API](#the-plugin-api)
- [Decided for now](#decided-for-now)

## Introduction

Core can record every furni, pet, bot and currency that changes **owner**, **place** or
**existence** into one table, `asset_movement_outbox`, for a plugin to consume (an audit ledger, a
rollback tool, dupe detection). Core says what moved, from where, to where, why and who did it;
what that means is the plugin's business.

**Status:** the record, the recorder, the table, the switch, the wallet, the inventory's
furni, trades, the marketplace and the room's placements, pickups and use-ups, the holders (wired
chests, jukeboxes), borrowed furni, pets, bots and starter rooms, and the dispatcher that hands
the rows to plugins and prunes them (below).

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

A movement's cause is its row's own: one operation may hold more than one code (a sale and its
fee, the categories of one vault claim), and the operation id is what ties them. Each source, sink
and fee has a code of its own, so what entered, left or was taken per channel is a sum by code:

| Class | Codes |
| --- | --- |
| Sources (currency made) | `vault.<category>` (one per earnings category, below), `reward.achievement`, `reward.quest`, `reward.daily_task`, `reward.track`, `reward.campaign_calendar`, `reward.online`, `catalog.voucher`, `use.credit_furni`, `use.crackable`, `staff.give_currency` |
| Sinks (currency spent) | `catalog.purchase`, `catalog.gift`, `catalog.room_ad`, `catalog.targeted_offer`, `catalog.ltd_raffle`, `catalog.habbicon`, `camera.purchase`, `camera.publish`, `group.create`, `market.tokens`, `market.fee`, `room.rent`, `wired.chest_upgrade`, `reward.track_premium`, `staff.give_currency` (a negative `:give`) |
| Transfers (between players) | `trade`, `catalog.gift` (the present, below), `market.buy` and `market.redeem` (through the offer), `wired.chest_deposit` and `wired.chest_withdraw` (through the chest) |

A code that is both tells which by its direction (`None` -> `Wallet` or `Wallet` -> `None`), and a
refund is a credit under its charge's code. The vault names a payout's category the way Flash's earnings view does:
`vault.tutorial`, `vault.dailygift`, `vault.achievements`, `vault.marketplace`, `vault.habboclub`
(HC payday), `vault.levelprogression`, `vault.roombundlesales`, `vault.bonusbag`,
`vault.donation`, `vault.surprise`, `vault.snowstorm`, `vault.games`, `vault.wiredchest`,
`vault.agency`; `vault.claim` is a payout of a category it does not know. Core pays no HC payday,
daily gift, level progression or recycler prize of its own: they reach a player only through a
vault deposit (`IIncomeRewardService.DepositAsync`), so their codes are the vault's. Wired gives
furni, never currency (`wired.give_reward`).

Fees: the marketplace's is a movement of its own in the sale (`market.fee`, below). Core has no
donation or credit transfer, no silver trade fee and no premium on credit furni (a credit furni
redeems at its face value, and what it cost is its catalogue price, `catalog.purchase`).

Caps: a balance has none but `int.MaxValue`. A credit that would cross it is refused whole and
records nothing, and the caller is told (a vault claim and a marketplace redeem stay waiting), so
no surplus is ever cut off and lost unrecorded.

## Who writes the rows

Only the primitives below write a furniture, pet, bot or balance row, so nothing changes owner,
place or existence without its movement. `AssetWriteGuardTests` (`Turbo.Tests/Database`) reads
the compiled server and fails when any other type adds, updates, deletes or builds one of those
rows, sets one of their properties (or a pet's or bot's through `IInventoryUnitEntity`), or
writes `furniture`, `pets`, `bots` or `player_currencies` in SQL, naming the type, the method
and the write. A lambda, an async method or a local function counts as the type it is in.

| Writer | Why it may |
| --- | --- |
| `FurnitureCustody` | The only writer of who holds a furni and where; each transition with its movements. |
| `InventoryUnitModule<TEntity,TSnapshot>`, `InventoryPetModule`, `InventoryBotModule` | The only writer of where a pet or a bot is; the two modules write the rows of its create and return. |
| `PlayerWalletGrain` | The only writer of balances, each change with its movement and receipt (the pending reward amount is an announcement, not a balance). |
| `RoomPersistenceGrain` | A room's buffered furni position and data (on a row that names no owner or room), its pets' stats and its bots' settings: moves inside a room are not movements. |
| `RoomGrain` | A room deletion's backstop for the pets and bots still in it, sent home with their movements. |
| `JukeboxGrain` | A disk's place in the playlist, in the unit of work of the custody move that put it there. |
| `BotProvisioner` (`Turbo.LoadBots`) | Load-test accounts' starting credits: not players of the hotel, so not recorded. |

A new writer moves its change through one of these, or records its movement itself and joins the
list with why; a listed type that stops writing has to leave it (the test says which). Not
checked: migrations and the model snapshot, which change the schema and its data before the hotel
opens, and what the compiled code does not tie to these rows (a row handed to the untyped
`DbContext.Remove(object)`, a setter of `TurboEntity`, SQL built from pieces that never spell the
table).

## The wallet

Every `IPlayerWalletGrain` method that moves a balance takes the caller's `MovementCause`:
`TryDebitAsync`, both `CreditAsync`, `CreditRewardAsync` and `CreditAchievementAsync`. While
recording, each currency it moves is one movement (`Wallet` -> `None` for a debit, `None` ->
`Wallet` for a credit; asset id 0 and its `CurrencyKind`), written in the same transaction as the
balance. Every call is one transaction (`InTransactionAsync`) with a receipt in
`wallet_credit_receipts`: the plugin's reference for a referenced credit, and otherwise the cause's operation id and first seq (`#op:<seq>:<operation>`,
hashed when too long); a debit's receipt is its key behind `#debit:`, so a credit and a debit under one
reference or cause never answer for each other. The same call made again under the same cause, a retry, changes nothing:
a debit answers success, a credit true. A debit of several currencies is one operation, numbered
from the cause's first seq, and takes all of them or none. A debit the balance cannot cover, a
refused credit and a replayed credit write nothing, and a refused one keeps no receipt. A
referenced debit (`TryDebitAsync(requests, reference, cause, ct)`) keeps the plugin's reference
behind `#debit:` as its receipt, in the same table as the referenced credits, and answers a repeat as succeeded
with `AlreadyApplied` set.

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
| `vault.<category>`, `vault.claim` | an earnings vault payout, one movement per category and currency under one operation per claim (cause ref: the claim) |
| `market.tokens`, `market.buy`, `market.redeem` | marketplace tokens, a purchase (cause ref: the offer) and its refund, a redeemed sale (cause ref: the offer) |
| `room.rent` | renting a space (cause ref: the furni) and its refund |
| `use.credit_furni`, `use.crackable` | a redeemed credit furni, a cracked crackable's credits (cause ref: the furni) |
| `wired.chest_upgrade`, `wired.chest_withdraw` | a chest's upgrade price and its refund, credits a chest pays out (actor 0; cause ref: the chest transaction) |
| `reward.track`, `reward.quest`, `reward.daily_task`, `reward.achievement` | reward track prizes (cause ref: track and prize), quest, daily task and achievement rewards |
| `reward.campaign_calendar` | a campaign calendar door's gift (cause ref: `calendar:<campaign id>:<day>`) |
| `reward.track_premium` | a reward track's premium bought (cause ref: the track) |
| `reward.online` | currency for time online, [online rewards](online-rewards.md) (actor: the player; cause ref: the currency) |
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
| `DestroyInInventoryAsync` | `Inventory` -> `None`, with a snapshot | `ConsumeFurnitureAsync`, `TakePostItSheetAsync` (a pad's last sheet) |
| `TransferBetweenInventoriesAsync` | `Inventory` -> `Inventory`, all or none | `TransferFurnitureAsync` |
| `SetExtraDataInInventoryAsync` | none: nothing changes hands; the row is written only while the inventory holds it | `TakePostItSheetAsync`, `ReturnPostItSheetAsync` (a pad's sheets left) |

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
| `use.credit_furni`, `use.crackable`, `use.present`, `use.effect_box`, `use.clothing`, `use.pet_product`, `use.pet_package`, `use.monsterplant_seed`, `pet.breed` | the furni a use takes out of the room (cause ref: the furni; `pet.breed`: the nest) |

## Holders, made-in-room and borrowed furni

A wired chest and a jukebox hold furni through the custody, in their own unit of work (a chest
writes its transaction row with the move):

| Transition | Movement | Called by |
| --- | --- | --- |
| `PutInHolderAsync` | `Inventory` -> `Holder` (the holder's id, held for its owner; a chest stamps its transaction) | a chest deposit, a disk put in a jukebox |
| `TakeOutOfHolderAsync` | `Holder` -> `Inventory` | a chest withdrawal, a disk taken out |
| `DestroyInInventoryAsync` (in the caller's unit of work, all or none) | `Inventory` -> `None`, with a snapshot | credit furni a chest turns into its balance |
| `CreateInRoomAsync` | `None` -> `Room` (where it stands), with a snapshot | a note stuck to a wall from a post-it pad, a starter room's furni |
| `SendHomeLeftInRoomAsync` | `Room` -> `Inventory` for every row still in the room | a room deletion's backstop |
| `BorrowIntoRoomAsync`, `GiveBackBorrowedAsync` | a `BorrowedFurni` (known by its room and the id the room gave it; owner 0) `None` -> `Room`, then `Room` -> `None` | a Builders Club borrow; its pickup, a bulk return, a use |

A chest's credits are a currency nobody owns while it holds them (`ChestCredits`, the chest's id):
a credit deposit is the credit furni destroyed, then the credits `None` -> `ChestCredits` (after
the furni's seqs); a withdrawal is `ChestCredits` -> `None` at seq 0 and the wallet's credit at
seq 1 of the same operation; credits the wallet refused come back `None` -> `ChestCredits` under
the withdrawal's refund cause.

| Cause | Where |
| --- | --- |
| `wired.chest_deposit`, `wired.chest_withdraw` | chest moves (actor 0; cause ref: the chest transaction) |
| `jukebox.add`, `jukebox.remove` | a disk in or out (cause ref: the jukebox; `jukebox.remove` actor 0) |
| `room.post_it` | a sheet of a post-it pad stuck to a wall, by a builder or on a room's post-it wall (actor: the player; cause ref: the pad; owner: the room's owner): the note made in the room, destroyed at the next seq when the room refuses it. A pad's other sheets are its sheet count, not furni, so they move nothing; its last sheet is the pad itself, used up at seq 0 before the note. A sheet no note came of is given back under the operation's refund (`ForRefund`): a pad used up comes back as a new pad of one sheet |
| `onboarding.starter_room` | a new player's starter room furnished (actor: the player; cause ref: the room) |
| `onboarding.gift` | a new player's pick of the new user gift offer: its furni and pets (actor: the player; cause ref: the step, `nux:<day>:<step>`) |
| `room.bc_borrow` | a borrow (cause ref: the offer); the give-back carries the cause of what removed it |
| `room.delete` | the backstop, an operation of its own after the hand-overs: furni, then pets, then bots |

## Pets and bots

The inventory's unit module is the only writer of where a pet or a bot is; each change is one
unit of work with its movement (`AssetKind.Pet` with the pet's type as definition, `AssetKind.Bot`
with none; custody only, no snapshot and no position).

| Change | Movement | Causes |
| --- | --- | --- |
| create | `None` -> `Inventory` | `catalog.purchase` and the others of a catalogue offer (pets, then bots, then furni of one offer), `reward.track`, `product.claim`, `pet.breed`, `use.pet_package`, `use.monsterplant_seed` (after the used-up furni) |
| check out | `Inventory` -> `Room` | `pet.place`, `bot.place`; a placement that fails gives it back under the placement's refund cause |
| return | `Room` -> `Inventory` | `pet.pickup`, `bot.pickup`, `room.delete` |
| delete | `Inventory` or `Room` -> `None` | `pet.compost`, `staff.delete_bot` (actor 0: the panel does not say who) |

## Trades

A trade is one operation (`trade`, actor: the player whose confirm completed it; cause ref: the
room it was made in): the initiator's items to the partner from seq 0, then the partner's items to
the initiator. Each side's list is its half's rows (`From` owner: who offered it), so a side that
offered nothing has no rows and a one-sided trade reads as one. When the second half fails, the
first is handed back under the same operation after both, with the cause ref `compensation`, so a
half trade shows as such.

## Gifts

A catalogue gift is one operation (`catalog.gift`, actor: the sender; cause ref: the offer): the
sender's price (`Wallet` -> `None`), then the present made into the receiver's inventory (`None` ->
`Inventory`, owner: the receiver) and its content into the present (`None` -> `Holder`, the
present's id, held for the receiver). Opening it (`use.present`, cause ref:
the present) takes the content `Holder` (that present) -> `Inventory` (the opener, who may hold the
present by a trade) and the present `Room` -> `None`. So sender, receiver and contents are one
chain from the purchase to the opening, keyed by the present's id. A staff gift (`staff.gift`) is
the same without the price.

## The marketplace

Every move names its offer:

| Cause | Movements |
| --- | --- |
| `market.list` | one operation per listing: each item `Inventory` -> `None` (with its snapshot), then, once the offer rows exist, each item `None` -> `MarketplaceOffer` (the offer's id) under the offer's id as cause ref. A listing that cannot be saved gives the items back under the listing's refund cause. |
| `market.buy`, `market.fee` | the price (seq 0); with the claim that marks the offer sold, the item off its offer `MarketplaceOffer` -> `None` (seq 1, with its snapshot), the price onto it, credits `None` -> `MarketplaceOffer` (seq 2), and the marketplace's fee off it again, `MarketplaceOffer` -> `None` under `market.fee` (seq 3, none for a fee of 0), so the offer holds the seller's revenue; then the item made again `None` -> `Inventory` for the buyer (seq 4). All under the offer's id. The item is a new row: the offer id and the snapshot link it to the listing. A buyer who lost the race writes no claim. |
| `market.cancel` | an offer taken back (or expired): the item off its offer (seq 0), with the delete that claims it, then made again in the seller's inventory (seq 1), under the offer's id. |
| `market.redeem` | one operation per sale: the revenue off its offer `MarketplaceOffer` -> `None` (seq 0), written with the sale marked redeemed, and the wallet's credit (seq 1) under the reference `marketplace:<offer>`, credited first. A credit that fails leaves the sale waiting for the next redeem; a redeem stopped between the two pays nothing twice (the reference is paid once), and the next writes the revenue off the offer under its own operation. |

What an offer holds therefore always comes off it again: its item when it is sold or taken back,
its revenue when it is redeemed. The offer keeps the listed item's id for this
(`marketplace_offers.furniture_id`); an offer listed before it was kept records no item leaving it.

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
   most once an hour (again at once after a full batch), whatever the plugins have read; only
   the server's own currency ledger, while it is registered, holds it back to its cursor (below). The
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
| `CurrencyLedger` | true | Whether the server keeps its own currency ledger while recording (below). |

These apply after a restart; `Enabled` and `CurrencyLedger` are live.

### The server's currency ledger

The server is a consumer itself: `core.currency-ledger` keeps every currency movement that leaves
or reaches a player's wallet in `currency_ledger`, for good, which the outbox is not. A row holds
the player, the currency, the amount (negative when it left), the cause and its ref, the actor,
and the other side of the move (nowhere, another wallet, an offer, a chest); a movement between
two wallets is a row for each. Its cursor is in `movement_consumer_cursors`, written in the same
save as its rows, and `(operation_id, seq, player_id)` is unique, so a batch delivered twice is
kept once. The admin panel's **Currency** log reads it. An achievement credit keeps its receipt
in `achievement_wallet_receipts` instead, under its award key, with what it paid.

It is the hotel's record of currency history: a plugin that wants it reads it rather than keeping
a second copy from the outbox. So the dispatcher never prunes a row the ledger has not read while
the ledger is registered, whatever the retention says. Ids pruned before it read them (it was
not registered while a plugin's dispatcher pruned) are kept as one `ledger.gap` row: no player, no
amount, the first and last id as its cause ref, so a hole in the history shows where it is.

It is registered only while `Enabled` and `CurrencyLedger` are both on, so a hotel that does not
record reads nothing, as before. While it is registered the dispatcher runs, and so prunes the
outbox after `OutboxRetentionHours` up to what the ledger has read. Furni, pets and bots are not
in it: a plugin still reads those from the outbox.

## Reversing a movement

A plugin that undoes movements (a ledger rolling back a duped trade) moves assets back through
`IAssetReversalService` (`Turbo.Primitives/Movements`), taken from the host services like the
consumer registry. Core decides nothing about *what* to undo; it guarantees that a reversal
cannot make an item that is also somewhere else, nor take what is not there.

```csharp
var reversals = services.GetRequiredService<IHostServices>()
    .GetRequiredService<IAssetReversalService>();

var result = await reversals.MoveAsync(
    new ReversalRequest
    {
        OperationId = $"ledger:plan-{plan.Id}-{step.Seq}", // this reversal's idempotency key
        ReversedOperationId = movement.OperationId,      // the operation it undoes
        ActorPlayerId = staffId,                         // must hold items.reverse
        Reason = "duped trade",
        Asset = movement.Asset,
        Expected = movement.To,   // where the movement left it
        Target = movement.From,   // where it was before
    },
    ct
);
```

| Expected -> Target | Done by |
| --- | --- |
| furni: inventory(P) -> inventory(Q) | `IInventoryGrain(P).TransferFurnitureAsync` |
| furni: inventory(P) -> none (a dupe taken back) | `IInventoryGrain(P).ConsumeFurnitureAsync` |
| furni: none -> inventory(Q) (made again from `Recreate`) | `IInventoryGrain(Q).GrantFurnitureAsync`, under a **new id** |
| currency: wallet(P) -> none (taken back) | `IPlayerWalletGrain(P).TryDebitAsync(requests, reference, ...)` |
| currency: none -> wallet(Q) (given back) | `IPlayerWalletGrain(Q).CreditAsync(kind, amount, reference, ...)` |
| a borrowed furni, a pet or a bot; a furni in or into a room, a holder or the marketplace | refused: `Unsupported` |

- **Compare and set.** Only the owner, kind and place id of `Expected` are compared. The grain's
  own guarded write refuses an item no longer held where the request says (`NotWhereExpected`);
  a balance too short to take back is `NotWhereExpected` too. A target that is the same place, a
  player that does not exist or a definition that does not is `TargetInvalid`.
- **Once per operation.** A receipt in `asset_reversal_receipts` (operation id unique, the
  operation undone, actor, reason, asset, outcome) is claimed before the move and settled after
  it: the same operation asked again answers `AlreadyApplied` and moves nothing. A call that finds
  an unsettled receipt (a crash, or a call still under way) answers `AlreadyApplied` when the furni
  already stands at the target and moves it otherwise; for a furni made again it cannot tell, so
  it answers `NotWhereExpected` and keeps the receipt rather than make a second one. A currency
  goes through the wallet's referenced debit or credit (reference `#rev:<operation>`), so the
  wallet pays a retry once. A refused reversal keeps no receipt and can be asked again.
- **A movement of its own.** While recording, the move is written like any other: the request's
  operation id, cause `reversal`, the operation undone as its cause ref, the actor as its actor.
  With recording off the reversal moves the asset all the same and writes no movement.
- **Permission.** The actor must hold `items.reverse`, which no wildcard grants (`docs/permissions.md`
  §7). A plugin is trusted server code already, so this is attribution and defence in depth; the
  plugin adds its own node on top.

## The plugin API

Consumers and `IAssetReversalService` are part of plugin API 1 (`"ApiVersion": 1` in a
manifest, `PluginApi.VERSION`): no plugin outside this project has been published, so the API
stays 1 while it changes. Every `IPlayerWalletGrain` and `IInventoryGrain` method that moves an
asset (`TryDebitAsync`, `CreditAsync`, `GrantFurnitureAsync`, `ConsumeFurnitureAsync`,
`TransferFurnitureAsync`, the present, pet and bot methods) takes a `MovementCause`: a plugin
passes one of its own (`MovementCause.New(code, actor, causeRef)`, a code of at most 48
characters; core's are in `MovementCauses`).

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
