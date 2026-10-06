# Observing room events from a plugin

A plugin can watch what happens in rooms by declaring an `IRoomEventListener`. There is nothing to
register by hand: when the plugin loads, Turbo Cloud scans its assembly (the same scan that finds
commands and room object logic), builds each listener with the plugin's services and gives it to
every room. When the plugin unloads or hot-reloads, the listeners are removed and, if they are
`IDisposable`, disposed.

```csharp
public sealed class WelcomeListener(ILogger<WelcomeListener> logger) : IRoomEventListener
{
    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        if (evt is AvatarMovedEvent moved)
            logger.LogDebug("Avatar {Id} moved to {X},{Y}", moved.ObjectId, moved.ToX, moved.ToY);

        return Task.CompletedTask;
    }
}
```

The class must be public, concrete and top level in the plugin assembly (the scan skips anything
else), and its constructor is resolved from the plugin's service provider.

## What a listener hears

Every event the room publishes: `PlayerEnterEvent`, `PlayerLeftEvent`, `PlayerClickedAvatarEvent`,
`PlayerClickedTileEvent`, `AvatarWalkOnFurniEvent`, `AvatarWalkOffFurniEvent`,
`AvatarPerformsActionEvent`, `RoomItemUsedEvent`, `RoomItemClickedEvent`, and the rest under
`Turbo.Primitives.Rooms.Events`. Two events exist for observers only:

| Event | Published when |
| --- | --- |
| `AvatarMovedEvent` (`FromX`, `FromY`, `ToX`, `ToY`) | An avatar's tile changes: a walked step, a teleport, a push or carry, a roller. Once per change; never in a tick where the avatar stayed put. |
| `AvatarIdleChangedEvent` (`IsIdle`) | An avatar falls asleep after the room's idle timeout (`true`) or wakes through its next action (`false`). Transitions only. |

Both name the avatar by its room index (`ObjectId`), so bots and pets report the same way players
do. For a player, `CausedBy.PlayerId` is the player id. These two go to registered listeners only,
not to the room's own systems (wired, game, roller...), and are not even built while no listener is
registered, so a room with no plugin listener pays nothing for them.

## Rules for a listener

- A listener runs inside the room grain's turn. Keep it quick.
- Never await a call that comes back to the room that is publishing to you (a `RoomGrain` method,
  or a player grain call that ends in that room). The room would be waiting on itself.
- For slow work (database, HTTP, grain calls), copy what you need out of the event and hand it to
  a `Channel<T>` or a task, then return. Events are immutable records, so they are safe to pass on.
- A listener that throws is logged at error level (listener, event and room id) and skipped. The
  room and the other listeners carry on. Do not rely on this for flow control.
- Listeners are called in registration order, after the room's own systems.

## Where it lives

- `IRoomEventListenerRegistry` (`Turbo.Primitives/Rooms`) holds the registered listeners; it is
  copy-on-write, so a room mid-publish never sees a half-changed list.
- `RoomEventListenerFeatureProcessor` (`Turbo.Rooms`) is the assembly scan.
- `RoomEventModule.PublishAsync` delivers to the room's systems and then the registry's listeners;
  `PublishToRegisteredAsync` is the observer-only path.
