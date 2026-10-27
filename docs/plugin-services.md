# Replace a host service

Some of the hotel's services are a whole feature behind one contract: the campaign calendar, who
may trade, how many free games a player has left. A plugin replaces one by registering its own
implementation in its services. While the plugin is live, everything in the hotel that calls the
contract calls the plugin's: core's packet handlers, the login, the admin API, scheduled work and
other plugins. When it unloads, the hotel's own is back.

```csharp
public sealed class AdventPlugin : TurboPlugin
{
    protected override void Services(IServiceCollection services) =>
        services.AddSingleton<ICampaignCalendarService, AdventCalendar>();
}
```

That is all. Nothing else is registered or called.

## Which services

A contract a plugin may replace is an interface marked `[ReplaceableService]`
(`Turbo.Primitives.Plugins`):

| Contract | What it decides | Called by |
| --- | --- | --- |
| `ICampaignCalendarService` (`Turbo.Primitives.Campaigns`) | the calendar a player is sent at login, what a door gives, the calendars staff edit (`docs/campaign-calendar.md`) | the door and staff door handlers, the login, the admin API's campaign calendars |
| `IPlayerTradePolicy` (`Turbo.Primitives.Trading`) | what a player may do in a trade (`TradeStandingType`: locked, no pass, receive only, full) | trades as they open, wired chests before they move anything, the marketplace before a sale |
| `IGameAccessPolicy` (`Turbo.Primitives.Games`) | how many games a player has left (`docs/plugin-games.md`) | the game centre's directory and joins |

A plugin that registers a host service that is not on this list keeps it to itself: its own classes
get it, the hotel never does. The server logs that as a warning naming the service as the plugin
loads (`registers its own ..., which this host does not let a plugin replace`).

A policy that decides one step of an action (a purchase, a room entry, a placement, a sign-up) is a
policy, not a replacement: several plugins' run in turn (`docs/plugin-policies.md`).

## How it works

- The hotel registers each replaceable contract through its **slot** (`AddReplaceable<TContract,
  TDefault>()` in `Turbo.Contracts.Plugins`). What its container gives for the contract forwards
  every call to the implementation in use at that moment. A grain, a handler or the admin API built
  before the plugin loaded calls the plugin's once it is live, and the hotel's again once it has
  gone; nothing holds on to a replaced implementation, so an unloaded plugin's assembly can go.
- The plugin's implementation is built from its own services as it loads (it takes the plugin's
  services and the hotel's by constructor, as any class it registers does), before it starts: one
  that cannot be built fails the load. It goes into the slot once the plugin has started, and comes
  out as it stops (after its own `StoppingAsync`), before anything else it registered.
- It is a singleton: built once per load, whatever lifetime it was registered with.
- `IGameAccessPolicy` is also found as a game is, as a public class in the plugin's assembly
  (`docs/plugin-games.md`); the two ways are the same slot.

## Two plugins

The slot is a stack. When two live plugins replace one contract, the one that loaded last is used,
and the other's comes back when it unloads (not the hotel's). The server logs it as a conflict
(`Conflict: second replaces ICampaignCalendarService, which first replaced too; ...`), and the
extension inspector (`GET /api/plugins/{key}/contributions`) lists the replacement under kind
`service` on each plugin and the clash in its conflicts. A plugin that reloads goes on top again.

## Adding to the one replaced

A replacement that changes part of a feature and leaves the rest to the hotel takes the contract's
slot, `IServiceSlot<T>`, by constructor and calls `Under(this)`: the implementation it replaced,
another plugin's or the hotel's own.

```csharp
public sealed class AdventCalendar(IServiceSlot<ICampaignCalendarService> slot)
    : ICampaignCalendarService
{
    // Staff edit the hotel's calendars as before; only opening a door is the plugin's.
    public Task<ImmutableArray<CampaignCalendarDefinition>> ListAsync(CancellationToken ct) =>
        slot.Under(this).ListAsync(ct);

    // ...
}
```

The contract through the hotel (`ctx.Host<T>()`, `IHostServices`) is the same for the plugin that
replaced it: the implementation under its own, as `Under(this)` gives, never the plugin's own
(calling itself would recurse until the stack overflows, which takes the whole server down). It is
worked out at each call, so one asked for in `StartingAsync`, before the replacement is in the slot,
is right once it is. When a later plugin replaces it in turn, the earlier one still reaches the one
it replaced, not the later one: each call through the hotel goes further down the stack. Other
plugins and the hotel get the one in use.

The contract by constructor is the plugin's own: a replacement that takes it (directly or through
another of the plugin's services) needs itself to be built, and the load fails with a circular
dependency. `slot.Current` is the implementation in use (the plugin's own while it is on top),
`slot.Default` the hotel's own and `slot.Owner` the plugin whose is in use (null for the hotel's).

## Making a service replaceable (core)

A host service becomes replaceable by marking its contract and registering it through its slot:

```csharp
[ReplaceableService]
public interface ICampaignCalendarService { ... }

services.AddReplaceable<ICampaignCalendarService, CampaignCalendarService>();
```

`AddReplaceable` refuses a contract that is not an interface marked `[ReplaceableService]`, and
`ReplaceableServiceTests` fails for a marked contract any host module registers directly (a direct
registration would ignore every plugin's). A contract fits when it is a single implementation of a
whole behaviour that hotels may want different; a decision several plugins share is a policy.
Callers take the contract by constructor as before.
