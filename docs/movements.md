# Asset movements

Core can record every furni, pet, bot and currency that changes **owner**, **place** or
**existence** into one table, `asset_movement_outbox`, for a plugin to consume (an audit ledger, a
rollback tool, dupe detection). Core says what moved, from where, to where, why and who did it;
what that means is the plugin's business.

**Status:** the record, the recorder, the table, the switch and the wallet (below). The inventory,
trade, marketplace, room, holder, pet and bot paths are wired up one by one in later changes, and
so are the dispatcher that hands the rows to a plugin and the pruning.

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
| `market.tokens`, `market.buy`, `market.redeem` | marketplace tokens, a purchase (cause ref: the offer) and its refund, redeemed sales |
| `room.rent` | renting a space (cause ref: the furni) and its refund |
| `use.credit_furni`, `use.crackable` | a redeemed credit furni, a cracked crackable's credits (cause ref: the furni) |
| `wired.chest_upgrade`, `wired.chest_withdraw` | a chest's upgrade price and its refund, credits a chest pays out (actor 0; cause ref: the chest transaction) |
| `reward.track`, `reward.quest`, `reward.daily_task`, `reward.achievement` | reward track premium and prizes, quest, daily task and achievement rewards |
| `staff.give_currency` | `:give` (actor: the staff member; one operation per player) |

## Decided for now

These follow the design's recommendations and wait on review:

- Delivery is a same-transaction outbox, not an event published after the commit: a record that can
  go missing silently is worse than none for an audit or a rollback.
- Primitives will take a required `MovementCause` (operation id, cause, cause ref, actor) rather
  than an ambient context, so a path that forgets one does not build.
- Badges, effects, clothing and subscriptions are out of scope; pets and bots are tracked for
  custody only.
- In-room moves are not recorded.
- A refund is an operation of its own whose cause ref is the charge's operation id, not the
  charge's operation: the two can never collide on (operation, seq).
- A cause names the action's actor as the player who did it (the buyer, the earner); wired and
  other server-driven moves are actor 0. `:give` has no execution id to use as its cause ref yet.
