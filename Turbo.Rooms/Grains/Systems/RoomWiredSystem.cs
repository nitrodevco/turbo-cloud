using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

internal sealed class RoomWiredSystem(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomAvatarModule roomAvatarModule,
    RoomMapModule roomMapModule
) : IRoomEventListener
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomAvatarModule _roomAvatar = roomAvatarModule;
    private readonly RoomMapModule _roomMap = roomMapModule;

    private readonly WiredGraph _wiredGraph;

    private int _tickMs => _roomConfig.AvatarTickMs;

    public async Task ProcessAvatarsAsync(long now, CancellationToken ct)
    {
        if (now < _state.NextAvatarBoundaryMs)
            return;

        while (now >= _state.NextAvatarBoundaryMs)
            _state.NextAvatarBoundaryMs += _tickMs;
    }

    public Task OnRoomEventAsync(RoomEvent @event, CancellationToken ct) =>
        HandleRoomEventAsync(@event, ct);

    private async Task HandleRoomEventAsync(RoomEvent @event, CancellationToken ct)
    {
        if (!_wiredGraph.NodesByEventType.TryGetValue(@event.GetType(), out var nodes))
            return;

        foreach (var node in nodes)
        {
            //
        }
    }
}
