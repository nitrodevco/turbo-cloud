using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Logging;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Mapping;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Avatars;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomAvatarModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule,
    IRoomAvatarFactory roomAvatarFactory,
    IRoomObjectLogicFactory objectLogicFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IRoomAvatarFactory _roomAvatarFactory = roomAvatarFactory;
    private readonly IRoomObjectLogicFactory _objectLogicFactory = objectLogicFactory;

    private int _nextObjectId = 0;

    private void InvokeAvatar(RoomObjectId objectId)
    {
        if (!_state.AvatarsByObjectId.TryGetValue(objectId.Value, out var avatar))
            throw new InvalidOperationException("Avatar not found in room state.");

        var current = _roomMap.GetTileIdForAvatar(avatar);
        var currentFlags = _state.TileFlags[current];

        if (!currentFlags.Has(RoomTileFlags.Sittable) || !currentFlags.Has(RoomTileFlags.Layable))
        {
            // disable sitting/laying if tile doesn't support it
        }
    }

    public async Task<IRoomAvatar> CreateAvatarFromPlayerAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct = default
    )
    {
        var avatar = _roomAvatarFactory.CreateAvatarFromPlayerSnapshot(
            objectId: RoomObjectId.From(_nextObjectId++),
            snapshot: snapshot
        );

        await AddAvatarAsync(avatar, ct);

        avatar.SetPosition(
            x: _state.Model?.DoorX ?? 0,
            y: _state.Model?.DoorY ?? 0,
            z: 0.0,
            rot: _state.Model?.DoorRotation ?? Rotation.South
        );

        var tileId = _roomMap.GetTileIdForAvatar(avatar);

        _state.TileAvatarStacks[tileId].Add(avatar.ObjectId.Value);

        await _roomMap.ComputeTileAsync(tileId);

        _ = _roomGrain.SendComposerToRoomAsync(
            new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] },
            ct
        );

        return avatar;
    }

    private async Task<IRoomAvatar> AddAvatarAsync(
        IRoomAvatar avatar,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(avatar);

        if (!_state.AvatarsByObjectId.TryAdd(avatar.ObjectId.Value, avatar))
            throw new InvalidOperationException("Failed to add avatar to room state.");

        await AttatchLogicIfNeededAsync(avatar, ct);

        return avatar;
    }

    private async Task AttatchLogicIfNeededAsync(IRoomAvatar avatar, CancellationToken ct)
    {
        if (avatar.Logic is not null)
            return;

        var logicType = "default_avatar";
        var ctx = new RoomAvatarContext(_roomGrain, this, avatar);
        var logic = _objectLogicFactory.CreateLogicInstance(logicType, ctx);

        if (logic is not IMovingAvatarLogic avatarLogic)
            throw new TurboException(TurboErrorCodeEnum.InvalidLogic);

        avatar.SetLogic(avatarLogic);

        await avatarLogic.OnAttachAsync(ct);
    }
}
