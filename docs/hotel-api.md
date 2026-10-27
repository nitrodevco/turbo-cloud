# The hotel API

`IHotel` (`Turbo.Contracts.Hotel`) is the running hotel as a plugin uses it: its players, rooms,
wallets, inventories, catalog, notices and clock. A plugin takes it by constructor and never needs
a grain, a session or a table for the everyday actions. Its tests use `FakeHotel`
(`Turbo.Testing`), the same API in memory with the same rules, and assert on what the plugin did.

Without it, every plugin that wanted tests wrote its own adapter: an interface of the hotel actions
it uses, a host class over `IGrainFactory.GetPlayerWalletGrain`, `ISessionGateway`,
`GetRoomDirectoryGrain` and `TurboDbContext`, and a fake. Those adapters were the same verbs over
and over (send to a player, pay, find a player, names of players, who is online, give furni, give a
badge, whisper, the room's owner, public rooms, club). They are this API now.

```csharp
public sealed class DailyBonus(IHotel hotel)
{
    public async Task PayAsync(PlayerId player, CancellationToken ct)
    {
        var day = hotel.Clock.GetUtcNow().ToString("yyyyMMdd");
        var paid = await hotel.Wallet.CreditAsync(player, "credits", 50, $"bonus:{player}:{day}", "bonus.daily", ct);

        if (paid == WalletOutcome.Done)
            await hotel.Notify.AlertAsync(player, "bonus.paid", "You got %0% credits!", ["50"], ct);
    }
}
```

```csharp
[Fact]
public async Task A_bonus_is_paid_once_a_day()
{
    var hotel = new FakeHotel();
    var alice = hotel.Players.Add("Alice", online: true);
    var bonus = new DailyBonus(hotel);

    await bonus.PayAsync(alice, ct);
    await bonus.PayAsync(alice, ct);

    hotel.Wallet.AssertCredited(alice, "credits", 50);
    hotel.Notify.AssertAlerted(alice, "bonus.paid");

    hotel.Clock.Advance(TimeSpan.FromDays(1));
    await bonus.PayAsync(alice, ct);

    hotel.Wallet.AssertCredited(alice, "credits", 100);
}
```

## Where it comes from

- Any class the plugin's assembly declares (a command, a message handler, a web route, an admin
  page, a room listener) takes `IHotel` by constructor, like any host service.
- A service the plugin registers itself in `ConfigureServices` takes it too: the host puts the
  hotel in every plugin's own container.
- It is DI only. There is no static `Hotel.Wallet`: plugins load in their own load contexts and a
  static would be state shared across them.

## Where each member may be called from

Every member carries `[HotelCall(...)]`, and its guide is this table. The rule test
`HotelApiTests` fails a member without one.

| Context | Means | Members |
| --- | --- | --- |
| `Anywhere` | in memory; fine inside any turn | `Players.OnlineIds`, `Wallet.CurrencyExists`, `Inventory.FindFurniture`/`Furniture`, `Catalog.Offers`, `Clock` |
| `NotFromItsGrain` | asks the grain of the player or room it is given; never await it inside that grain's own turn (a room's listener or command awaiting its own room waits on itself) | sends, whispers, room players, current room, club, permissions, wallet changes and balances, furni grants, badges, alerts and notifications, wired events, room holds and watches |
| `OffGrain` | reads the database (or waits on other plugins); never inside a room's turn (listener, command, furni logic): copy what you need out of the turn and read it after | player and room reads, search, toplists, public rooms, circulation, catalog edits and publish, staff notices |

## The areas

| Area | Members |
| --- | --- |
| `Players` | `GetAsync(id)`, `FindAsync(name)` (any case), `NamesAsync(ids)`, `SearchAsync(prefix, take)` (at most 50), `OnlineIds()`, `CurrentRoomAsync(id)`, `ClubAsync(id)` (active, streak days), `OnlineTimeAsync(id)` (time online in all and in the session running now; the total is saved as a session goes, every `Turbo:Players:AchievementOnlineIntervalSeconds`, and counts from the version that added it), `RespectsAsync(id)` (respects received from players, the infostand's figure, and given to players; pets' not counted), `HasPermissionAsync(id, node)`, `TopByAsync(stat, take, skip)` (respects, achievement score or a currency's balance; at most 100; system accounts never ranked), `CountAsync(activeSince)` (logged in since then or online now; all with null; system accounts not counted), `RecentAsync(activeSince, take, skip)` (the same players, last login first, at most 100, each with their look and whether online), `FindByIdentityAsync(provider, id)`, `LinkIdentityAsync(id, identity, replace)` and `UnlinkIdentityAsync(id, provider)` (see below), `SendAsync(id, message or composer)` (false when offline) |
| `Rooms` | `GetAsync(id)` (name, owner, owner's name, on the public list), `PublicIdsAsync()`, `PlayersAsync(id)`, `SendAsync(id, message or composer)` and `WhisperAsync(id, player, text)` (false, and the room not loaded, when no one is in it), `InvokeFurniLogicAsync(id, itemId, message)` (a message to your own furni logic there, `docs/plugin-wired.md`), `LoadAsync(id)`, `HoldAsync(id, holder, lease)` and `ReleaseAsync(id, holder)` (keep a room no one is in loaded and awake), `WatchAsync(player, id, envelope, lease)` and `UnwatchAsync(player, id)` (send a player another room's packets inside your message); holds and watches are your plugin's, end on their own and when it unloads (`docs/plugin-room-watch.md`) |
| `Wallet` | `CurrencyExists(name)`, `BalanceAsync`, `CreditAsync` and `DebitAsync(player, currency, amount, reference, cause)` |
| `Inventory` | `FindFurniture(name)`, `Furniture(id)`, `GrantFurnitureAsync(player, definition, cause)`, `GrantFurnitureAsync(player, definition, reference, cause)` (once per reference: `Granted`, `AlreadyGranted`, `Refused`), `HasBadgeAsync`, `GiveBadgeAsync` (false when they had it) |
| `Catalog` | `Offers(page)` (the live catalog), `CirculationAsync(definitions)` (held by players, system accounts left out), `MoveOfferAsync`, `SetPageExpiryAsync`, `PublishAsync` (the panel's catalog editor, with its checks and history) |
| `Notify` | `AlertAsync(player, textKey, defaultText, parameters)` (the hotel's alert, its text from the hotel's texts), `NotificationAsync(player, type, parameters)` (a client notification whose `notification.<type>` variable the hotel or the plugin's `external_variables.json` has), `StaffNoticeAsync(notice)` (`docs/plugin-notices.md`) |
| `Navigator` | `Public`: the navigator's Public tab through the admin panel's own editor. `CategoriesAsync()` (every category in order, hidden ones too, its key, name, shown, opens closed, thumbnails, staff picks, rooms in order); `EnsureCategoryAsync(key, defaults)` makes the plugin's category under `key` (`<plugin-key>:<name>`, `HotelPublicKey`) once, from `defaults` (name, shown, opens closed, thumbnails, rooms), and after that answers it as staff left it, so call it every start; `AddRoomAsync(key, room, first)` (false when it is in it), `RemoveRoomAsync(key, room)` (false when it was not), `OrderRoomsAsync(key, rooms)` (every room once). Only the plugin's own categories, by key; a missing key, a room that does not exist or a bad order throws `ArgumentException`. Each change is in the command log as the plugin's (source `plugin`); players see it with their next search. To order a category's rooms at search time instead (busiest first), see `IPublicRoomOrder` in `docs/plugin-policies.md` |
| `Wired` | `FireAsync(room, event, triggerer)` and `FireForPlayerAsync(player, event)`: a plugin's wired event for its own triggers (`docs/plugin-wired.md`); false, and the room not loaded, when no one is in it |
| `Status` | `Current`: whether the hotel is open (`HotelAvailabilitySnapshot`: `Phase` `Open`, `MaintenanceScheduled`, `Maintenance`, `ShutdownScheduled` or `ShuttingDown`, `AtUtc` when a countdown ends, its `Reason`, `BlocksLogin` while it is closed to players without `hotel.maintenance.bypass`), in memory; each change (scheduled, called off, run out) is a `HotelAvailabilityChangedEvent` (`Previous`, `Current`) a plugin's `IEventHandler<T>` hears (`docs/plugin-policies.md`) |
| `Clock` | a `TimeProvider`: the hotel's time; `FakeHotel.Clock` moves only when the test moves it |

Reads are query methods with their own limits, not `DbSet`s: a plugin never builds a query on a
core table, so a core table can change without breaking it.

### Identity links

A player signs in to the public site with the Discord account linked to them (`docs/public-site.md`).
A plugin that proves a Discord account belongs to an existing player (an imported account's
returning owner: a code shown in the hotel, a staff check) links it with
`LinkIdentityAsync(player, HotelIdentity.Discord(id, name), replace: false)`: `Linked`,
`AlreadyLinked`, `TakenByAnother` (another player has it), `PlayerHasAnother` (the player has
another Discord account), `NoSuchPlayer` or `Unsupported` (a service the hotel keeps no links for;
`discord` is the one). An identity is one player's and a player has one of each service, as the
database's unique indexes keep it. `replace: true` is staff's override: it moves the identity from
whoever had it and drops the player's other one; the plugin gates the route or command that asks
for it with a permission node (`docs/permissions.md`). A link made raises
`PlayerIdentityLinkedEvent`, and the hotel's configured owner (`Turbo:Owner:DiscordId`) is made
owner as at sign-up. The plugin's proof is its own: the hotel takes the link as given.

### Wallet rules

A currency is named as in the hotel's currency types, in any case. Every change is keyed by a
reference of the plugin's (`<plugin-key>:<id>`, at most 128 characters), so a retry after a crash
cannot pay twice, and is recorded in the movement log under the plugin's cause code
(`docs/movements.md`). `Done`, `AlreadyDone` (made before under that reference; treat it as done)
or `Refused` (no such currency, an amount below 1, a balance that cannot cover a debit or would
overflow; no receipt is kept, so the same reference can go through later). A credit and a debit
keep apart receipts: a debit under a credit's reference takes it back, once. A blank reference, one
too long or one starting with `#op:`/`#debit:` throws (`HotelReference.ThrowIfInvalid`): that is a
bug in the plugin, not something a player did (`docs/wallet-idempotent-credit.md`).

A furni grant takes a reference the same way when giving it twice would be wrong (a prize, a
claim, a reward a retry may ask for again): the item and its receipt (`furniture_grant_receipts`,
per player and reference) are written in one transaction, so a repeat makes nothing and answers
`AlreadyGranted`, and the reference is the movement's cause ref in the movement log. The grant
without a reference stays for things given once per call.

## FakeHotel

`new FakeHotel()` (optionally from a start time) holds what the test adds and records what the
plugin does:

- set up: `Players.Add(name, online)` (a `FakePlayer` whose `IsSystemAccount`, `Respects`,
  `AchievementScore`, `Club`, `LastLoginAt` and `Permissions` the test sets; it passes as its `PlayerId`),
  `Rooms.Add(name, owner, isPublic)`, `Rooms.Enter(room, player)`, `Wallet.Set(player, currency,
  amount)`, `Wallet.AddCurrency(name)` (credits, duckets and diamonds are there), `Inventory.AddFurniture(name)`,
  `Inventory.Hold(...)`, `Catalog.AddOffer(...)`, `Status.Set(snapshot)` (open until then),
  `Navigator.Public.AddStaffCategory(name, rooms)` (a staff one; a test changes any `FakePublicCategory`'s
  name, shown, rooms as staff would); ids count up from 1, or from `Players.NextId`,
  `Rooms.NextId` and `Inventory.NextFurnitureId` when the test sets them;
- fail: `Wallet.FailNext(count, throws)` and `Inventory.FailNext(count, throws)` make the next
  credits, debits, grants or badge gifts fail as a hotel write that failed does (refused, nothing
  changed, no receipt kept, so a retry under the same reference goes through), or throw the
  exception given, as a crash mid-call does;
- read back: `Players.SentTo(player)`, `Players.Identities`, `Rooms.SentTo(room)`, `Rooms.WhispersTo(player)`,
  `Wallet.Movements`, `Inventory.Grants`, `Notify.Alerts`, `Notify.StaffNotices`,
  `Catalog.Publishes`, `Wired.Fired`, `Navigator.Public.All` / `.Category(key)` (a room in a shown
  category is on `Rooms.PublicIdsAsync`, as on a hotel);
- assert: `Wallet.AssertCredited/AssertDebited/AssertUntouched`, `Players.AssertSent<T>/AssertNothingSent`,
  `Rooms.AssertSent<T>/AssertWhispered`, `Rooms.IsHeld(room)`, `Rooms.Watching(player)`,
  `Rooms.WatchEnvelope(player, room)` (a `FakeRoom`'s `Banned` and `DoorClosed` keep a watcher
  out), `Inventory.AssertGranted/AssertHasBadge`,
  `Notify.AssertAlerted/AssertNotAlerted`, `Wired.AssertFired<TEvent>/AssertNothingFired`. A failure throws `HotelAssertionException` with what
  happened instead, so it works in any test framework.

`Turbo.Testing.dll` ships in the server's bin folder beside `Turbo.Contracts.dll`; a plugin's test
project references it from `$(TurboBin)` (the template's does).

## Why the fake cannot drift

`Turbo.Tests/Hotel/HotelContract.cs` is one set of scenarios (wallet references and refusals,
names in any case, search, toplists without system accounts, rooms no one is in, grants and
circulation, grants under a reference, badges, sends to offline players) run twice: `FakeHotelContractTests` on `FakeHotel`,
`HostHotelContractTests` on the host's implementation over a database and the real wallet,
inventory and badge grains. A rule changed in one and not the other fails one of them. Holds and
watches live in the room and presence grains, which that host world fakes: their rules are tested
on the grains (`RoomHoldTests`, `PresenceRoomWatchTests`, `PluginRoomLeaseTests`) and on the fake
(`FakeRoomWatchTests`), both from the same `RoomWatchLimits`.

## Adding to it

Add a member when two plugins need the same hotel action, not before: the API is small on
purpose. A new member gets its `[HotelCall]`, the host's implementation in `Turbo.Main/Hotel`, the
fake's in `Turbo.Testing`, a contract scenario, and a line in the table above. Each area is its own
registration (`AddTurboHotel`), so a host can put another implementation of one area in front.
