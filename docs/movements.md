# Asset movements

Core can record every furni, pet, bot and currency that changes **owner**, **place** or
**existence** into one table, `asset_movement_outbox`, for a plugin to consume (an audit ledger, a
rollback tool, dupe detection). Core says what moved, from where, to where, why and who did it;
what that means is the plugin's business.

**Status:** the record, the recorder, the table and the switch. Nothing calls the recorder yet:
the wallet, inventory, trade, marketplace, room, holder, pet and bot paths are wired up one by one
in later changes, and so are the dispatcher that hands the rows to a plugin and the pruning. Until
then, the table stays empty whatever the switch says.

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

## Decided for now

These follow the design's recommendations and wait on review:

- Delivery is a same-transaction outbox, not an event published after the commit: a record that can
  go missing silently is worse than none for an audit or a rollback.
- Primitives will take a required `MovementCause` (operation id, cause, cause ref, actor) rather
  than an ambient context, so a path that forgets one does not build.
- Badges, effects, clothing and subscriptions are out of scope; pets and bots are tracked for
  custody only.
- In-room moves are not recorded.
