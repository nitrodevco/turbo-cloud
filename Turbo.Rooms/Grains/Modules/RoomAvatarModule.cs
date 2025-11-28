using System;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomAvatarModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule,
    IRoomAvatarFactory roomAvatarFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IRoomAvatarFactory _roomAvatarFactory = roomAvatarFactory;

    private int _nextObjectId = 0;

    public async Task<IRoomAvatar> CreateAvatarFromPlayerAsync(PlayerSummarySnapshot snapshot)
    {
        var avatar = _roomAvatarFactory.CreateAvatarFromPlayerSnapshot(
            objectId: RoomObjectId.From(_nextObjectId++),
            snapshot: snapshot
        );

        if (!_state.AvatarsByObjectId.TryAdd(avatar.ObjectId.Value, avatar))
        {
            throw new InvalidOperationException("Failed to add avatar to room state.");
        }

        return avatar;
    }
}
