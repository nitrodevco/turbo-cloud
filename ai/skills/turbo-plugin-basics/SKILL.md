---
name: turbo-plugin-basics
description: Build the server half of a Turbo plugin - the plugin class and its services, the hotel API (IHotel), chat commands, permission nodes, settings, scheduled tasks, room event listeners, post-commit events and the plugin's own tables. Use when adding or changing any C# in a Turbo plugin's server/ folder.
---

# Turbo plugin basics

## When to use this skill

Adding server code to a plugin: a command, a node, a setting, a service, a timer, a listener, a
table. Furni, wired, policies, messages, web routes and MCP tools have skills of their own.

## The plugin class

Optional: a plugin with none still loads, and everything below is found without it. Write one to
register the plugin's own services or to run code as it starts or stops.

<!-- compiled as the plugin "demo" -->

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;

namespace Demo;

public sealed class DemoPlugin : TurboPlugin
{
    // Only the plugin's own types: host services (IHotel, IGrainFactory, ILogger<T>) come by constructor.
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<Greetings>();

    // Runs once the hotel is up (and at once after a reload). ctx.Stopping is cancelled as it unloads.
    protected override Task HotelReadyAsync(IPluginContext ctx, CancellationToken ct) =>
        Task.CompletedTask;
}
```

## The hotel API

`IHotel` (`Turbo.Contracts.Hotel`) is how a plugin acts on the hotel: `Players`, `Rooms`, `Wallet`,
`Inventory`, `Catalog`, `Notify`, `Navigator`, `Wired`, `Status`, `Clock`. Never a grain, a session
or a core table for these.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Hotel;
using Turbo.Primitives.Players;

namespace Demo;

public sealed class Greetings(IHotel hotel)
{
    public const string GREETED = "demo.greeted";

    /// <summary>Pays 10 credits once per player and day, and tells them.</summary>
    public async Task<bool> GreetAsync(PlayerId player, CancellationToken ct)
    {
        var day = hotel.Clock.GetUtcNow().ToString("yyyyMMdd");
        var paid = await hotel.Wallet.CreditAsync(player, "credits", 10, $"demo:greet:{player}:{day}", "demo.greet", ct);

        if (paid != WalletOutcome.Done)
            return false;

        await hotel.Notify.AlertAsync(player, GREETED, "Hello %0%, here are 10 credits.", [player.ToString()], ct);

        return true;
    }
}
```

- Every wallet change has a reference of the plugin's (`<key>:<id>`, at most 128 characters): a
  retry under it answers `AlreadyDone` and never pays twice.
- `Players.GetAsync`, `FindAsync`, `SearchAsync`, toplists and every read of a table are
  `OffGrain`: not from a room's turn (a command, furni logic, a listener). Sends, whispers, wallet
  changes and grants are `NotFromItsGrain`: never on the room or player you are running in.

## A chat command

`npm run make command hug` writes this shape. Arguments are a record bound by the hotel (an
`IRoomPlayer` in the room, `int`, `string`, an enum, `RestOfLine` for the rest, `?` or a default for
optional); a line that does not fit gets the usage text. Replies come from the hotel's texts
`command.<name>.<status>`, `DefaultTexts` until staff set them.

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Demo;

public sealed record WaveArguments(IRoomPlayer Target, int Times = 1);

/// <summary><c>:wave name [times]</c>: says a wave over the executor's head.</summary>
[Command("wave", Description = "Wave at someone")]
[RequiresPermission(DemoPermissions.WAVE)]
public sealed class WaveCommand : ICommand<WaveArguments>
{
    private const string WAVED = "waved";
    private const string TOO_MANY = "too_many";

    public IReadOnlyDictionary<string, string> DefaultTexts { get; } =
        new Dictionary<string, string> { [WAVED] = "You waved at %0%.", [TOO_MANY] = "At most %0% waves." };

    public async ValueTask<CommandResult> ExecuteAsync(ICommandContext ctx, WaveArguments arguments, CancellationToken ct)
    {
        if (arguments.Times is < 1 or > 3)
            return CommandResult.Fail(TOO_MANY, "3");

        // ctx.Room is the room the command was typed in, in its own turn: its members are safe here.
        await ctx.Room.SayAsync(ctx.Executor, $"*waves at {arguments.Target.Name}*", shout: false, ct);

        return CommandResult.Done(WAVED, arguments.Target.Name);
    }
}

/// <summary>The plugin's permission nodes; a hotel grants or denies them per rank.</summary>
public sealed class DemoPermissions : IPermissionNodeSource
{
    public const string WAVE = "demo.command.wave";

    public string? Prefix => "demo";

    public IEnumerable<PermissionNodeDefinition> Nodes => [new(WAVE, "Wave with :wave", GrantedByDefault: true)];

    public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
}
```

- `[RequiresPermission]` is required on every command. A node starts with the plugin's key.
- Results: `CommandResult.Done(status, params)` / `Fail(status, params)` whisper the text
  `command.<name>.<status>` (`%0%`, `%1%` are the params); `CommandResult.Ok` answers nothing (the
  command said or showed something itself); `Confirm(status, ...)` asks the executor to confirm.
- `ctx.Room`: `SayAsync`, `WhisperAsync`, `NoticeAsync`, `KickAsync`, `MuteAsync`, `FloorItemsAt(x, y)`.
  `ctx.Executor` is the player who typed it (`PlayerId`, `Name`), `ctx.ArgumentText` the raw rest.
- Keep the work in a service (tested with `FakeHotel`) and the command thin.

## Settings

A `[PluginSettings("<Section>")]` class is bound and listed on the admin panel's Settings page;
take `IOptionsMonitor<T>` and read `CurrentValue` each time (a save reaches the plugin at once).
`npm run make setting rate --type int --default 5` adds one. Settings are hotel policy only.

```csharp
using Turbo.Contracts.Plugins;

namespace Demo;

/// <summary>The plugin's settings (each summary is the panel's help line).</summary>
[PluginSettings("Demo")]
public sealed class DemoSettings
{
    /// <summary>Credits the daily greeting pays.</summary>
    public int GreetingCredits { get; init; } = 10;
}
```

## A scheduled task

```csharp
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Plugins.Scheduling;

namespace Demo;

/// <summary>Every Monday 18:00 (cron, UTC), once per cluster; listed on the panel with Run now.</summary>
[Schedule("0 18 * * MON")]
public sealed class WeeklyReset : IScheduledTask
{
    public Task RunAsync(ScheduledTaskRun run, CancellationToken ct) => Task.CompletedTask;
}
```

`[Every("00:05:00")]` for an interval; `PerServer = true` for what each server holds in memory.

## Room event listeners and post-commit events

- `IRoomEventListener.OnRoomEventAsync(RoomEvent evt, ct)` hears every room's events
  (`PlayerEnterEvent`, `AvatarMovedEvent`, `RoomItemUsedEvent`... in `Turbo.Primitives.Rooms.Events`)
  in the room's turn: return at once, hand slow work to a task.
- `IEventHandler<T>` (`Turbo.Events.Registry`) hears post-commit events off the turn, once the change
  is saved: `PurchaseCompletedEvent`, `PlayerLoggedInEvent`, `PlayerRegisteredEvent`,
  `RoomEnteredEvent`... Use `EventId` to act once. See the turbo-policies skill.

## Tables

`npm run make migration Init` writes `server/Database/<Name>Db.cs` (a `PluginDbContextBase<T>`)
and a migration; `manifest.json` needs `TablePrefix`. The hotel migrates them as the plugin loads.
The context is scoped: take `IServiceScopeFactory` in singletons, routes and tasks. Guide:
`database.md` ("A plugin's tables").

## Check

`npm run check` after every change; `npm test`. Guides: `plugin-loading.md`, `hotel-api.md`,
`commands.md`, `permissions.md`, `settings.md`, `plugin-scheduler.md`, `plugin-room-events.md`.
