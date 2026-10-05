# Plugin events

A plugin reacts to the hotel by implementing `IEventHandler<T>` for an event `T`. Handlers are
found by assembly scan and run on the global `EventSystem`.

## Player connect and disconnect

`SessionGateway` publishes both, in `Turbo.Primitives.Players.Events`:

| Event | Raised |
|---|---|
| `PlayerConnectedEvent(PlayerId, SessionKey)` | Once, when a session is bound to a player at login. |
| `PlayerDisconnectedEvent(PlayerId, SessionKey)` | Once, when that binding is removed, however the connection ended. |

A session that never logged in raises neither. A player who logs in again while connected
raises a connect for the new session and, when the old one is removed, a disconnect for it, so
count sessions rather than assuming one disconnect means the player is offline. A handler that
throws is logged and does not break login or teardown.

```csharp
public sealed class Greeter : IEventHandler<PlayerConnectedEvent>
{
    public ValueTask HandleAsync(PlayerConnectedEvent e, EventContext ctx, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
```
