# Testing

- [Introduction](#introduction)
- [Running Tests](#running-tests)
- [Testing Against FakeHotel](#testing-against-fakehotel)
    - [Setting the Scene](#setting-the-scene)
    - [Asserting What Happened](#asserting-what-happened)
    - [Making Things Fail](#making-things-fail)
    - [Time](#time)
- [Testing Scheduled Tasks](#testing-scheduled-tasks)
- [Testing MCP Tools](#testing-mcp-tools)
- [Testing the Client Module](#testing-the-client-module)
- [Testing a Plugin as the Hotel Loads It](#testing-a-plugin-as-the-hotel-loads-it)
- [Testing Core](#testing-core)

## Introduction

A plugin is tested without a server. `Turbo.Testing`, which ships in the server's bin folder,
holds `FakeHotel`: the [hotel API](hotel-api.md) in memory, with the same rules as the real
hotel. Your plugin takes `IHotel` by constructor, so in a test you hand it a `FakeHotel`, run the
code, and assert on what it did to the hotel: who was paid, who was alerted, what was sent.

For a test of the whole plugin, loaded from its sources as the hotel loads it, `TestHotel` plays the
game against it: see [The Test Hotel](plugin-testing.md). Projects made from the plugin template
come with a test project on it already (`tests/<Name>.Tests`, xUnit) and client tests
(`client/tests`, `node --test`).

## Running Tests

```shell
npm test                                   # the server tests and the client tests
dotnet test tests/Skills.Tests             # the server tests only
dotnet test tests/Skills.Tests --filter SkillBookTests
```

The test project imports `$(TurboBin)/plugin-sdk/Turbo.Plugin.Tests.props`, from the bin folder of
the server you build against: it references the test kit and the server's own assemblies, so the
fake and the test hotel are always the same version as the hotel you run.

## Testing Against FakeHotel

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
    var ct = TestContext.Current.CancellationToken;
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

### Setting the Scene

`new FakeHotel()` starts empty (credits, duckets and diamonds exist). Add what the test needs:

| Call | Adds |
| --- | --- |
| `Players.Add(name, online)` | a `FakePlayer`, which passes as its `PlayerId`; set its `Permissions`, `Club`, `Respects`, `IsSystemAccount` |
| `Rooms.Add(name, owner, isPublic)`, `Rooms.Enter(room, player)` | a room, and a player in it |
| `Wallet.Set(player, currency, amount)`, `Wallet.AddCurrency(name)` | a balance, a currency |
| `Inventory.AddFurniture(name)`, `Catalog.AddOffer(...)` | a furniture definition, a catalogue offer |
| `Status.Set(snapshot)` | the hotel closed for maintenance, or shutting down |

Ids count up from 1, or from `Players.NextId`, `Rooms.NextId` and `Inventory.NextFurnitureId` when
you set them.

### Asserting What Happened

| Area | Asserts | Reads |
| --- | --- | --- |
| `Wallet` | `AssertCredited`, `AssertDebited`, `AssertUntouched` | `Movements` |
| `Players` | `AssertSent<T>`, `AssertNothingSent` | `SentTo(player)`, `Identities` |
| `Rooms` | `AssertSent<T>`, `AssertWhispered`, `IsHeld(room)`, `Watching(player)` | `SentTo(room)`, `WhispersTo(player)` |
| `Inventory` | `AssertGranted`, `AssertHasBadge` | `Grants` |
| `Notify` | `AssertAlerted`, `AssertNotAlerted` | `Alerts`, `StaffNotices` |
| `Wired` | `AssertFired<TEvent>`, `AssertNothingFired` | `Fired` |

A failed assertion throws `HotelAssertionException` saying what happened instead, so the fake
works with any test framework.

### Making Things Fail

`Wallet.FailNext(count, throws)` and `Inventory.FailNext(count, throws)` make the next writes fail as
a real one can: refused with nothing changed (so a retry under the same reference goes through), or
throwing the exception you pass, as a crash part-way would. Test that your plugin pays exactly once
when the first try fails.

### Time

`FakeHotel.Clock` is the hotel's `TimeProvider`, and it moves only when the test moves it:
`hotel.Clock.Advance(TimeSpan.FromDays(1))` or `hotel.Clock.Set(when)`. Start the hotel at a
chosen moment with `new FakeHotel(start)`. Read time from `hotel.Clock` in your plugin, never from
`DateTime.Now`, and every time-based rule is testable.

## Testing Scheduled Tasks

`NextRunOf(task)` says when the server would run a [scheduled task](plugin-scheduler.md) next,
by its schedule and the fake clock; `RunAsync(task)` runs it once as the server would:

```csharp
var hotel = new FakeHotel(saturdayNoon);
var job = new SkillsWeeklyReset(book);

Assert.Equal(mondayMidnight, hotel.NextRunOf(job));

hotel.Clock.Set(mondayMidnight);
await hotel.RunAsync(job, ct: ct);
```

## Testing MCP Tools

`McpToolTester` calls a plugin's [MCP tools](plugin-mcp-tools.md) as the hotel's MCP server does:
the same schema, checks, permissions and read-only rules, with no server.

```csharp
var hotel = new FakeHotel();
var staff = hotel.Players.Add("Staff");
staff.Permissions.Add("plugin.mcp.tool.find_player");

var mcp = new McpToolTester(new FindPlayerTool(hotel)).As(staff);

(await mcp.CallAsync("find_player", new { name = "al" })).AssertOk();
(await mcp.CallAsync("find_player", new { name = "al", take = 100 })).AssertInvalid("must be at most 50");
```

[MCP Tools](plugin-mcp-tools.md#3-testing-a-tool) lists everything the tester can check.

## Testing the Client Module

The template's client tests run with Node's own test runner, against the module's plain
TypeScript: keep what can be computed (reading a message's fields, a progress bar's share) in
functions apart from the React components, and test those.

```ts
import assert from 'node:assert/strict';
import { test } from 'node:test';

import { progress } from '../src/skill.ts';

void test('progress is the share of the level done, never past its end', () => {
    assert.equal(progress({ level: 1, experience: 150, needed: 100 }), 1);
});
```

## Testing a Plugin as the Hotel Loads It

`FakeHotel` tests your plugin's own classes one at a time. To test the plugin as a whole, use
`TestHotel` (also in `Turbo.Testing`): it compiles and loads the plugin from its sources as the
server does, then lets players say its commands, send its messages, buy through its policies, use
furni running its logic and wait for its scheduled tasks:

```csharp
var hotel = TestHotel.Create();
await hotel.LoadAsync<SkillsPlugin>();

var alice = hotel.Player("Alice");
await alice.SendsAsync(new TrainMessage());

Assert.Equal(new SkillMessage(1, 25, 100), alice.Should().HaveReceived<SkillMessage>());
```

[The Test Hotel](plugin-testing.md) covers everything it does, and what it leaves out (operator
commands, the room's own simulation). The template's test project uses it, and
`npm run make command` / `make message` write a test beside the piece they add.

## Testing Core

Core's own tests are in `Turbo.Tests` and build rooms, grains, sessions and packet round-trips
without a silo or a database (`RoomHarness`, `GrainHarness`, `SessionHarness`, `PacketHarness`, all
in `Turbo.Tests/Support`). Every change to behaviour comes with a test that fails without it; the
[Contribution Guide](contributions.md#tests) and
[`docs/patterns/UnitTestPattern.cs`](patterns/UnitTestPattern.cs) show the shape.

```shell
dotnet test Turbo.Tests/Turbo.Tests.csproj --filter RoomStackingTests
```
