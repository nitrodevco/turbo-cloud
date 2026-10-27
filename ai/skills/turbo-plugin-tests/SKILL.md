---
name: turbo-plugin-tests
description: Test a Turbo plugin without a server - xunit v3 tests in tests/<Name>.Tests against FakeHotel (the hotel API in memory with its rules and assertions), McpToolTester for MCP tools, policies and commands called directly, client logic with node --test - and run them with npm test. Use when writing or fixing any plugin test, or before calling a plugin change done.
---

# Testing a plugin

## When to use this skill

Every behaviour a plugin adds gets a test that would fail without it. Tests run with `npm test`
(`dotnet test tests/<Name>.Tests` and `node --test client/tests`), against the server's own
`Turbo.Testing.dll` from `$TurboBin`: no hotel, no database, no network.

## What earns a test

- The plugin's own rules: what a command, service, policy or tool decides, given hotel state.
- A bug that happened (the test reproduces it).
- Not: getters, constructors, defaults, the hotel's own behaviour, or a copy of a test that exists.

## Shape: logic in a service, tested with FakeHotel

Keep commands, handlers and routes thin; put the decision in a service the test builds directly.

<!-- compiled as the plugin "demo" -->

```csharp
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Demo;

/// <summary>Pays a player a prize once per reference, and tells them.</summary>
public sealed class Prizes(IHotel hotel)
{
    public const string WON = "demo.prize.won";

    public async Task<bool> PayAsync(PlayerId player, string prize, int credits, CancellationToken ct)
    {
        var paid = await hotel.Wallet.CreditAsync(player, "credits", credits, $"demo:prize:{prize}:{player}", "demo.prize", ct);

        if (paid != WalletOutcome.Done)
            return false;

        await hotel.Notify.AlertAsync(player, WON, "You won %0% credits!", [credits.ToString()], ct);

        return true;
    }
}
```

```csharp test
using System.Threading;
using System.Threading.Tasks;
using Turbo.Testing;
using Xunit;

namespace Demo.Tests;

public sealed class PrizesTests
{
    private readonly FakeHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_prize_is_paid_once_per_player_and_tells_them()
    {
        var alice = _hotel.Players.Add("Alice", online: true);
        var prizes = new Prizes(_hotel);

        Assert.True(await prizes.PayAsync(alice, "quiz", 50, Ct));
        Assert.False(await prizes.PayAsync(alice, "quiz", 50, Ct));

        _hotel.Wallet.AssertCredited(alice, "credits", 50);
        Assert.Equal("You won 50 credits!", _hotel.Notify.AssertAlerted(alice, Prizes.WON).Text);
    }

    [Fact]
    public async Task A_failed_payment_tells_nobody_and_can_be_retried()
    {
        var alice = _hotel.Players.Add("Alice");
        var prizes = new Prizes(_hotel);

        _hotel.Wallet.FailNext(1);

        Assert.False(await prizes.PayAsync(alice, "quiz", 50, Ct));
        _hotel.Notify.AssertNotAlerted(alice);
        Assert.True(await prizes.PayAsync(alice, "quiz", 50, Ct));
    }
}
```

## FakeHotel

- Set up: `Players.Add(name, online)` (a `FakePlayer`: `Permissions`, `Club`, `Respects`... it
  passes as its `PlayerId`), `Rooms.Add(name, owner, isPublic)`, `Rooms.Enter(room, player)`,
  `Wallet.Set(player, currency, amount)`, `Inventory.AddFurniture(name)`, `Catalog.AddOffer(...)`.
- Time: `hotel.Clock.Advance(TimeSpan)`: it never moves by itself.
- Failures: `Wallet.FailNext(count, throws?)`, `Inventory.FailNext(...)`.
- Read back and assert: `Wallet.AssertCredited/AssertDebited/AssertUntouched`,
  `Players.AssertSent<T>(player)`/`AssertNothingSent`, `Rooms.AssertSent<T>/AssertWhispered`,
  `Inventory.AssertGranted/AssertHasBadge`, `Notify.AssertAlerted/AssertNotAlerted`,
  `Wired.AssertFired<TEvent>(room)`, `Rooms.IsHeld/Watching/WatchEnvelope`. A failure throws
  `HotelAssertionException` saying what happened instead.
- One `FakeHotel` per test (a field is fine: xunit builds the class per test).

## Other kinds

- **Settings**: pass `IOptionsMonitor<T>` with a small static implementation (`CurrentValue => value`).
- **Policies**: call `DecideAsync(new RoomEntryRequest(player, room, Doorbell: false), ct)` and
  assert the decision.
- **Commands**: test the service the command calls; `ICommandContext` needs a room, so a command is
  checked by `npm run check` and on the dev hotel.
- **MCP tools**: `McpToolTester` (turbo-mcp-tools skill).
- **Client**: plain `.ts` logic with `node --test` in `client/tests/*.test.ts`.

## Run

`npm test` (or `dotnet test tests/<Name>.Tests --filter <Class>`), and `npm run check` for what the
hotel's compiler says. A test project that fails to find `Turbo.*` types means `TurboBin` points at
an older server: `node tools/host.mjs` says what to rebuild. Guide: `hotel-api.md` ("FakeHotel").
