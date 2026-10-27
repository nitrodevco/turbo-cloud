# Testing a Plugin

- [Introduction](#introduction)
- [A test hotel](#a-test-hotel)
    - [Loading the plugin](#loading-the-plugin)
    - [What the plugin is given](#what-the-plugin-is-given)
- [Players](#players)
    - [Commands](#commands)
    - [Messages](#messages)
- [Rooms and furni](#rooms-and-furni)
- [Purchases](#purchases)
- [Approvals](#approvals)
- [Time and scheduled tasks](#time-and-scheduled-tasks)
- [Settings and the database](#settings-and-the-database)
- [Web routes and admin endpoints](#web-routes-and-admin-endpoints)
- [Events and logs](#events-and-logs)
- [Assertions](#assertions)
- [Not in the kit](#not-in-the-kit)

## Introduction

A plugin's tests play the game against it: players who say its commands and send its messages,
purchases its policies are asked about, furni running its logic, a clock that moves when told. The
test kit (`Turbo.Testing`) ships with the server, beside its assemblies, and a plugin's test project
takes it from there:

```xml
<!-- tests/Skills.Tests/Skills.Tests.csproj -->
<Import Project="$(TurboBin)/plugin-sdk/Turbo.Plugin.Tests.props" />
<ItemGroup>
  <ProjectReference Include="../../server/Skills.csproj" />
</ItemGroup>
```

`Turbo.Plugin.Tests.props` references the kit, the server's assemblies a plugin builds on, the EF
Core SQLite provider at the server's EF Core version, and copies the server's `plugin-sdk/` beside
the tests. The test framework is yours (the template uses xUnit); the kit's assertions throw
`HotelAssertionException` and work under any. The kit is for tests only: it is not among what the
server compiles a plugin against, so a plugin's own sources cannot use it. Like the plugin SDK it
is versioned with the server.

`templates/turbo-plugin` (`dotnet new turbo-plugin`) has a test project on the kit, and
`npm run make command hug` / `npm run make message ping` write a test beside the piece they add
(`tests/<Name>.Tests/Commands/HugCommandTests.cs`).

## A test hotel

```csharp
public sealed class SkillsTests : IAsyncLifetime
{
    private readonly TestHotel _hotel = TestHotel.Create(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync() => await _hotel.LoadAsync<SkillsPlugin>();

    public ValueTask DisposeAsync() => _hotel.DisposeAsync();

    [Fact]
    public async Task Training_moves_up_a_level()
    {
        var alice = _hotel.Player("Alice");

        await alice.SendsAsync(new TrainMessage());

        Assert.Equal(new SkillMessage(1, 25, 100), alice.Should().HaveReceived<SkillMessage>());
    }
}
```

`TestHotel.Create(now)` makes an empty hotel, its clock at `now` (2026-01-01 12:00 UTC by
default). One test at a time; dispose it at the end.

### Loading the plugin

`await hotel.LoadAsync<TPlugin>()` loads the plugin whose class (or any type of its assembly) is
`TPlugin`, from the nearest folder above the test holding a `manifest.json` (or the folder given:
`LoadAsync<TPlugin>(folder)`):

1. its manifest, its sources (the manifest's `Sources`, `server/**/*.cs` by default), `gamedata/`
   and `wwwroot/` are copied as a hotel installs them;
2. the sources are compiled by the server's own compiler with the server's references, C# version
   and policy analyzer (`docs/plugin-loading.md`). A plugin that does not compile, or does what
   plugins may not (`TPP001`...), fails with `PluginLoadException`, its `Reasons` the server's own
   lines;
3. the plugin then loads as on a server (its services, settings, tables, hosted services, `StartingAsync`,
   `HotelReadyAsync`), and the server's registries take what its assembly declares: commands,
   named messages and their handlers, packet handlers, event handlers, policies, furni logic,
   permission nodes, scheduled tasks, web routes and admin endpoints. A registration the server
   refuses (a command name taken, a message the directory refuses) fails the load the same way;
4. it runs as the test's own build of those same sources (the `ProjectReference`), so the test
   names the plugin's types: `new TrainMessage()`, `plugin.Service<SkillBook>()`.

A test run compiles each plugin's sources once: the first hotel to load them compiles them (a few
seconds, most of it the compiler starting), and every later hotel (or one loading them at the same
time) takes that compile, checked and refused alike, and builds its own registrations, services,
tables and settings from it, so tests share nothing else. Sources changed in any way compile again.

`LoadAsync(folder)` with no build runs what the server compiled, in a context of its own, whose
types the test cannot name: for a test of the load itself. `hotel.UnloadAsync("skills")` unloads a
live plugin as disabling it does. A plugin that depends on another loads after it: load both.

`hotel.Plugins["skills"]` (or what `LoadAsync` returns) is the live plugin:

| Member | What |
| --- | --- |
| `plugin.Service<T>()` | one of the plugin's own services, as its classes are given it |
| `plugin.Settings<T>()` | its settings class now, as its classes read it |
| `plugin.Db<T>()` | a new one of its database contexts over the hotel's database (dispose it) |
| `plugin.Create<T>()` | a new one of its classes, built as the server builds them (its services, then the host's) |
| `plugin.Text(key)` | its text of `key` (`gamedata/external_texts.txt`) |

### What the plugin is given

| Host service | In a test hotel |
| --- | --- |
| `IHotel` (`docs/hotel-api.md`) | `hotel.Fake`, the `FakeHotel`: `hotel.Wallet`, `hotel.Inventory`, `hotel.Catalog`, `hotel.Notify`, `hotel.Wired`, and `hotel.Fake.Players` / `.Rooms` / `.Navigator` |
| `TimeProvider` | `hotel.Clock` (a `ManualClock`); a plugin that registers its own `TimeProvider` is given the hotel's |
| configuration | `hotel.Settings` |
| `IEventPublisher` | `hotel.Events` |
| `IHostApplicationLifetime` | started from the first |
| its tables | an in-memory SQLite database (the real SQL: keys, constraints, transactions); each context's tables are made from its model as it loads, where a server runs its migrations |
| anything else (a grain, the session gateway, a provider of core's) | a recording stand-in (`hotel.Host`) |

`hotel.Replace<T>(service)` gives every plugin loaded after it `service` as its own `T`, in place
of what it registers: a fake of its own gateway.

`hotel.Host` stands in for the host services the kit does not play. A stand-in answers every call
with nothing (a completed task, `default`, an empty array, another stand-in) and records it:

```csharp
hotel.Host.Use<IMyCoreThing>(new MyCoreThing());                       // a real one
hotel.Host.Answer<IPlayerCurrencyGrain>("GetBalanceAsync", call => Task.FromResult(10));
var asked = hotel.Host.CallsOf<IPlayerCurrencyGrain>("GetBalanceAsync");
```

A player's permission grain answers from the kit's players; a session's player is theirs.

## Players

`hotel.Player("bob")` is the online player called bob, added on first use, signed in on a session
of their own. A `TestPlayer` passes as their `PlayerId` and as the `FakePlayer` the hotel API
holds (`bob.Fake`: their club, figure, achievement score...).

| Member | What |
| --- | --- |
| `bob.InRoom(5)` | stands bob in room 5 (added on first use), out of any other |
| `bob.LeavesRoom()` | takes bob out of his room |
| `bob.WithPermissions("skills.admin")` | grants nodes (`*`: all of them) |
| `bob.AsStaff()` | every node |
| `await bob.SaysAsync(":skills")` | says a line in his room: a command runs (below) |
| `await bob.SendsAsync(new TrainMessage())` | his client sends a named message (below) |
| `await bob.SendsAsync(packet)` | his client sends a core packet (an `IMessageEvent`) to the plugins' handlers of it |
| `bob.Received` | everything he was sent, in order: what plugins sent through the hotel API and what his session was answered (a named message read back into its record) |
| `bob.Whispers` | the whispers he was sent, a command's reply among them |
| `bob.Notifications` | the client notifications he was shown |
| `bob.LastCommand` | what his last command came to (`CommandOutcome`) |
| `bob.Room` | the room he stands in |

A node a plugin grants by default (`GrantedByDefault: true`) is held by every player, as every
rank holds it on a hotel.

### Commands

`SaysAsync(":plant 3")` runs the command as a room does: a node it needs (`[RequiresPermission]`),
the room level it needs (the room's owner is its owner, a player holding `*` a moderator), its
arguments bound by the hotel's own binder, then the command. Each refusal is the hotel's reply,
whispered; a result's status is whispered from the plugin's texts (`command.plant.planted` in
`gamedata/external_texts.txt`), else core's, else the command's `DefaultTexts`. A line that is no
command is kept as said in the room.

```csharp
var bob = hotel.Player("bob").InRoom(5).WithPermissions("plugin.garden.plant");

await bob.SaysAsync(":plant");

bob.Should().HaveHadCommand(CommandOutcome.Completed);
bob.Should().HaveBeenWhispered("Planted 1 of 3.");
```

An operator command (`IOperatorCommand`), said by a holder of its node, leaves the room for the
hotel's own operator command runner, as on a server: the player's nodes are asked again, its
arguments bound, the plugins' vetoes asked (`CommandExecutingEvent`), then the command, given who
was in the room as the line was said. A command that reaches `Turbo:Commands:ConfirmAtPlayers`
players waits for core's `:confirm` (`hotel.Settings.Set("Turbo:Commands:ConfirmAtPlayers", 3)`
changes it for the test). Its replies are whispered, its notices to players are alerts
(`hotel.Notify.Alerts`), and a use the hotel writes down (by a holder of `command.log`, or a line
that named a group of players) is in `hotel.CommandLog.Entries`.

```csharp
var alice = hotel.Player("alice").InRoom(10).WithPermissions("events.event");

await alice.SaysAsync(":event Falling Furni");
alice.Should().HaveHadCommand(CommandOutcome.AwaitingConfirmation);

await alice.SaysAsync(":confirm");
alice.Should().HaveBeenWhispered("Falling Furni started");
```

### Messages

`SendsAsync(message)` writes the named message by its schema and reads it back, as the wire does
(a field the client and server read differently shows here), then hands it to the hotel's packet
pipeline: its sign-in and permission gates, then every plugin handler of it. What a handler
answers (`ctx.ReplyAsync`) is in `bob.Received`, read back into its record:

```csharp
await bob.SendsAsync(new WaterMessage(3));

bob.Should().HaveReceived<WateredMessage>(m => m.Litres == 6);
```

## Rooms and furni

`hotel.Room(5)` is room 5, added on first use (owned by a player called `Owner`). A `TestRoom`
passes as its `RoomId`.

| Member | What |
| --- | --- |
| `room.Players` | who stands in it |
| `room.Said` | the lines its code said (a command's `SayAsync`, a furni's), with who said them |
| `room.Sent` | what plugins sent everyone in it |
| `room.Actions` | what a command or furni asked of the room that the kit records (`Kick(Bob)`, `SetEffect(Bob, 4)`) |
| `room.Furni("garden_sprinkler", x, y)` | a furni running the plugin's `[FurniLogic("garden_sprinkler")]` class |

A `TestFurni` is the furni's `IFurniContext` (`docs/plugin-wired.md`) on an open floor of 10 by 10
tiles:

```csharp
var sprinkler = hotel.Room(5).Furni("garden_sprinkler");

await sprinkler.WalkOnAsync(bob);               // OnWalkOnAsync (OnAttachedAsync first)
await sprinkler.WalkOffAsync(bob);
await sprinkler.UseAsync(bob, param: 0);        // OnUseAsync; whether it handled it
await sprinkler.SendAsync(FurniLogicMessage.Of("water", new { litres = 2 }), bob);
await sprinkler.RunScheduledAsync();            // what it Schedule()d, the clock moved on to each

sprinkler.State.Should().Be(1);
hotel.Room(5).Should().HaveSaid("Splash!");
hotel.Room(5).Should().HaveFired<SprinkledEvent>();
```

`sprinkler.ExtraData["section"]` is its extra data for `ReadExtraData`; `Forwarded` the players it
sent to another room; a walk to tiles arrives at once.

Room holds and watches (`docs/plugin-room-watch.md`) are the hotel API's:
`hotel.Room(5).Should().BeHeld()`, `hotel.Fake.Rooms.Watching(bob)`.

## Purchases

`hotel.Purchases` buys as the catalogue does, up to the money: the offer's price for the quantity is
proposed, every plugin purchase policy is asked in turn as the hotel asks them (a refusal ends it, a
policy that throws refuses it), and an allowed purchase for oneself or a room ad is handed to the
plugin fulfilment that takes it. No money moves here: the decision is what the hotel would charge
and give.

```csharp
var offer = hotel.Catalog.AddOffer(pageId: 1, "throne", costCredits: 5);

await hotel.Purchases.BuyAsync(bob, offer, quantity: 2);
await hotel.Purchases.BuyAsync(bob, offer, kind: PurchaseKind.Gift, giftTo: alice);
await hotel.Purchases.BuyAsync(bob, TestPurchases.Offer(7, credits: 5, clubLevel: 1));

hotel.Purchases.Should().HaveRefused(7, CatalogPurchaseErrorType.RequiresHabboClub);
hotel.Purchases.Should().HaveAllowed(offer.Id).Decision.CostCredits.Should().Be(10);
```

## Approvals

`hotel.Approvals` is the admin panel's Approvals inbox (`docs/approvals.md`) over the server's own
approvals: what the plugin asks for with `IApprovals` is saved as on a server, and a staff member
approves or denies it. An approval is refused for the reasons a server gives (the approver does not
hold the request's node, it is their own, it was decided already or expired), and otherwise runs
the plugin's `ApprovalHandler<TArgs>` for its kind, once. A decision returns after the handlers of
the events it raised (`ApprovalDecidedEvent`, `ApprovalExecutedEvent`) have run.

```csharp
var bob = hotel.Player("bob").InRoom(5).WithPermissions("plugin.shop.refund");
var staff = hotel.Player("staff").WithPermissions("plugin.shop.refund.approve");

await bob.SaysAsync(":refund 50");
var asked = hotel.Approvals.Should().HavePending("shop.refund");

var decision = await hotel.Approvals.ApproveAsync(asked.Id, staff, note: "ok");   // or ApproveAsync("shop.refund", staff)
decision.Status.Should().Be(ApprovalDecisionStatus.Executed);
bob.Should().HaveBalance("credits", 50);

// Also: DenyAsync(id or kind, staff, note) (nothing runs), PendingAsync(kind?) (newest first),
// AllAsync(kind?) (in any state), GetAsync(id), and RequestAsync(request) to ask as core and test
// a handler on its own. hotel.Approvals.Decisions lists the decisions made, refused ones too.
```

A request expires as the clock moves past it: the next thing asked of `hotel.Approvals` finds it
expired, as the server's sweep each minute does, and the plugin hears of it through
`ApprovalDecidedEvent`:

```csharp
hotel.Clock.Advance(TimeSpan.FromHours(4));
hotel.Approvals.Should().HaveBeen("shop.refund", ApprovalState.Expired);
bob.Should().HaveBeenNotified("shop.refund.expired");   // what the plugin's event handler did
```

## Time and scheduled tasks

`hotel.Clock.Advance(by)` / `.Set(at)` move the clock the plugin reads. `hotel.Schedule` runs the
plugins' scheduled tasks (`docs/plugin-scheduler.md`) against it: each is due at the next time its
schedule names after it last ran (after its plugin loaded, before its first run).

```csharp
await hotel.Schedule.AdvanceAsync(TimeSpan.FromDays(3));   // runs each task as it comes due on the way
await hotel.Schedule.RunAsync("skills.SkillsWeeklyReset", manual: true);   // the panel's Run now

hotel.Schedule.Tasks;                       // the live tasks' keys
hotel.Schedule.NextRunOf("skills.SkillsWeeklyReset");
hotel.Schedule.Ran;                         // each run, by key and time
```

A run missed while a hotel was down is not a thing here.

## Settings and the database

```csharp
hotel.Settings.Set("Skills:ExperiencePerTrain", 60);   // reaches IOptionsMonitor at once, as a panel save does
plugin.Settings<SkillsSettings>().ExperiencePerTrain.Should().Be(60);

using var db = plugin.Db<SkillsDb>();
db.Players.Should().ContainSingle();
```

Set settings before `LoadAsync` for what the plugin reads as it starts.

## Web routes and admin endpoints

```csharp
var top = await hotel.Web.GetAsync("/plugins/skills/top?n=5", bob);       // signed in as bob; null: no one
var saved = await hotel.Web.PostAsync("skills/train", new { times = 2 }, bob);
top.Status.Should().Be(200);

var pages = hotel.Admin.Pages;
var nodes = hotel.Admin.Nodes;                                            // plugin.<key>.<name>
var list = await hotel.Admin.GetAsync("events/prizes", staff);           // 403 without the endpoint's node
var added = await hotel.Admin.PostAsync("events/prizes", new { name = "small" });
```

A route that is not anonymous answers 401 to no one signed in and 403 to a player without one of its
nodes, as the site does.

## Events and logs

```csharp
await hotel.Events.PublishAsync(new PlayerRegisteredEvent { ... });   // to the plugins' IEventHandler<T>s, awaited
await hotel.Events.SettledAsync();                                    // the post-commit events' handlers have run
hotel.Events.Should().HavePublished<PluginNoticeEvent>(n => n.Topic == "event.started");

hotel.Logs.Should().HaveNoWarnings();          // nothing failed and was passed over
hotel.Logs.Should().HaveLogged("Reloaded plugin skills");
```

## Assertions

| Assertion | Passes when |
| --- | --- |
| `bob.Should().HaveReceived<T>(match?)` | bob was sent a `T` (returns the last) |
| `bob.Should().NotHaveReceived<T>()` | he was sent none |
| `bob.Should().HaveBeenWhispered(text)` | a whisper to him holds `text` |
| `bob.Should().NotHaveBeenWhispered()` | none was whispered to him |
| `bob.Should().HaveBeenNotified(type)` | he was shown a notification of `type` |
| `bob.Should().HaveHadCommand(outcome)` | his last command came to `outcome` |
| `bob.Should().HaveBalance("credits", 50)` | his wallet holds that |
| `bob.Should().HaveBadge("ACH_1")` | he holds the badge |
| `bob.Should().BeInRoom(5)` | he stands in room 5 |
| `room.Should().HaveFired<TEvent>(match?)` | wired fired `TEvent` in the room |
| `room.Should().HaveSaid(text)` | its code said a line holding `text` |
| `room.Should().HaveBeenSent<T>()` | a plugin sent everyone in it a `T` |
| `room.Should().BeHeld()` / `NotBeHeld()` | a plugin holds it loaded |
| `room.Should().HaveActed("Kick(Bob)")` | a command or furni asked it for that |
| `hotel.Purchases.Should().HaveRefused(offerId, error?, reason?)` | its last purchase was refused so |
| `hotel.Purchases.Should().HaveAllowed(offerId)` | its last purchase was allowed (returns it) |
| `hotel.Approvals.Should().HavePending(kind)` | a request of `kind` waits for a decision (returns the newest) |
| `hotel.Approvals.Should().NotHavePending(kind?)` | none (of `kind`) does |
| `hotel.Approvals.Should().HaveBeen(kind, state)` | the newest request of `kind` is `state` (`Executed`, `Denied`, `Expired`, ...) |
| `hotel.Events.Should().HavePublished<T>(match?)` / `NotHavePublished<T>()` | an event was (not) published |
| `hotel.Logs.Should().HaveNoWarnings()` / `HaveLogged(text, level)` | the logs |

The `FakeHotel`'s own assertions (`hotel.Wallet.AssertCredited(bob, "credits", 50)`,
`hotel.Notify.AssertAlerted(bob, key)`, `hotel.Inventory.AssertGranted(...)`) are in
`docs/hotel-api.md`.

## Not in the kit

- Operator commands typed at the server console or run from the admin panel: a player says them.
- A room's own simulation (walking, rolling, wired stacks): a furni's walk arrives at once, and a
  wired event a plugin fires is recorded, not run through the room's boxes.
- Plain chat reaching plugin code (chat policies, room event listeners): `SaysAsync` runs commands only.
- The client module build (`client/src/index.tsx`): the client module's own tests cover it.
- An MCP tool's call that needs approval: `McpToolTester` runs the tool; test its approval as a
  request of its own with `hotel.Approvals`.
