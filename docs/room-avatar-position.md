# Reading a player's room position

Code outside a room's own turn (plugin packet handlers, background services) can read where a
player stands without loading a room snapshot. It is two calls: the player's presence grain says
which room they are in, and that room says where they are.

```csharp
var pointer = await grainFactory.GetPlayerPresenceGrain(playerId).GetActiveRoomAsync(ct);

if (pointer.RoomId > 0)
{
    var position = await grainFactory.GetRoomGrain(pointer.RoomId).GetAvatarPositionAsync(playerId, ct);

    if (position is { IsIdle: false })
        Console.WriteLine($"{position.X},{position.Y} z={position.Z} facing {position.Rotation}");
}
```

`IRoomGrain.GetAvatarPositionAsync` returns a `RoomAvatarPositionSnapshot` (room id, tile X and
Y, height Z, body `Rotation`, `IsIdle`, `IsWalking`), or `null` when the player has no avatar in
that room (never entered, already left, or the room just unloaded).

- It is a plain read inside the room's single-threaded turn: it changes nothing, and unlike a
  player's own requests it does not count as activity, so it never wakes a sleeping avatar or
  resets the idle timers.
- The answer is a snapshot of that moment; a walking avatar has moved on by the time it arrives.
  `X`/`Y` is the tile the avatar is on, not the walk's goal.
- It is asked by player id, so bots and pets are never returned.
