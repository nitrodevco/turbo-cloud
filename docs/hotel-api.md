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
| `NotFromItsGrain` | asks the grain of the player or room it is given; never await it inside that grain's own turn (a room's listener or command awaiting its own room waits on itself) | sends, whispers, room players, current room, club, permissions, wallet changes and balances, furni grants, badges, alerts and notifications, wired events |
| `OffGrain` | reads the database (or waits on other plugins); never inside a room's turn (listener, command, furni logic): copy what you need out of the turn and read it after | player and room reads, search, toplists, public rooms, circulation, catalog edits and publish, staff notices |

## The areas

| Area | Members |
| --- | --- |
| `Players` | `GetAsync(id)`, `FindAsync(name)` (any case), `NamesAsync(ids)`, `SearchAsync(prefix, take)` (at most 50), `OnlineIds()`, `CurrentRoomAsync(id)`, `ClubAsync(id)` (active, streak days), `HasPermissionAsync(id, node)`, `TopByAsync(stat, take, skip)` (respects, achievement score or a currency's balance; at most 100; system accounts never ranked), `SendAsync(id, message or composer)` (false when offline) |
| `Rooms` | `GetAsync(id)` (name, owner, owner's name, on the public list), `PublicIdsAsync()`, `PlayersAsync(id)`, `SendAsync(id, message or composer)` and `WhisperAsync(id, player, text)` (false, and the room not loaded, when no one is in it), `LoadAsync(id)` |
| `Wallet` | `CurrencyExists(name)`, `BalanceAsync`, `CreditAsync` and `DebitAsync(player, currency, amount, reference, cause)` |
| `Inventory` | `FindFurniture(name)`, `Furniture(id)`, `GrantFurnitureAsync(player, definition, cause)`, `HasBadgeAsync`, `GiveBadgeAsync` (false when they had it) |
| `Catalog` | `Offers(page)` (the live catalog), `CirculationAsync(definitions)` (held by players, system accounts left out), `MoveOfferAsync`, `SetPageExpiryAsync`, `PublishAsync` (the panel's catalog editor, with its checks and history) |
| `Notify` | `AlertAsync(player, textKey, defaultText, parameters)` (the hotel's alert, its text from the hotel's texts), `NotificationAsync(player, type, parameters)` (a client notification whose `notification.<type>` variable the hotel or the plugin's `external_variables.json` has), `StaffNoticeAsync(notice)` (`docs/plugin-notices.md`) |
| `Wired` | `FireAsync(room, event, triggerer)` and `FireForPlayerAsync(player, event)`: a plugin's wired event for its own triggers (`docs/plugin-wired.md`); false, and the room not loaded, when no one is in it |
| `Clock` | a `TimeProvider`: the hotel's time; `FakeHotel.Clock` moves only when the test moves it |

Reads are query methods with their own limits, not `DbSet`s: a plugin never builds a query on a
core table, so a core table can change without breaking it.

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

## FakeHotel

`new FakeHotel()` (optionally from a start time) holds what the test adds and records what the
plugin does:

- set up: `Players.Add(name, online)` (a `FakePlayer` whose `IsSystemAccount`, `Respects`,
  `AchievementScore`, `Club` and `Permissions` the test sets; it passes as its `PlayerId`),
  `Rooms.Add(name, owner, isPublic)`, `Rooms.Enter(room, player)`, `Wallet.Set(player, currency,
  amount)`, `Wallet.AddCurrency(name)` (credits, duckets and diamonds are there), `Inventory.AddFurniture(name)`,
  `Inventory.Hold(...)`, `Catalog.AddOffer(...)`;
- read back: `Players.SentTo(player)`, `Rooms.SentTo(room)`, `Rooms.WhispersTo(player)`,
  `Wallet.Movements`, `Inventory.Grants`, `Notify.Alerts`, `Notify.StaffNotices`,
  `Catalog.Publishes`, `Wired.Fired`;
- assert: `Wallet.AssertCredited/AssertDebited/AssertUntouched`, `Players.AssertSent<T>/AssertNothingSent`,
  `Rooms.AssertSent<T>/AssertWhispered`, `Inventory.AssertGranted/AssertHasBadge`,
  `Notify.AssertAlerted/AssertNotAlerted`, `Wired.AssertFired<TEvent>/AssertNothingFired`. A failure throws `HotelAssertionException` with what
  happened instead, so it works in any test framework.

`Turbo.Testing.dll` ships in the server's bin folder beside `Turbo.Contracts.dll`; a plugin's test
project references it from `$(TurboBin)` (the template's does).

## Why the fake cannot drift

`Turbo.Tests/Hotel/HotelContract.cs` is one set of scenarios (wallet references and refusals,
names in any case, search, toplists without system accounts, rooms no one is in, grants and
circulation, badges, sends to offline players) run twice: `FakeHotelContractTests` on `FakeHotel`,
`HostHotelContractTests` on the host's implementation over a database and the real wallet,
inventory and badge grains. A rule changed in one and not the other fails one of them.

## Adding to it

Add a member when two plugins need the same hotel action, not before: the API is small on
purpose. A new member gets its `[HotelCall]`, the host's implementation in `Turbo.Main/Hotel`, the
fake's in `Turbo.Testing`, a contract scenario, and a line in the table above. Each area is its own
registration (`AddTurboHotel`), so a host can put another implementation of one area in front.
