using System;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Avatars;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Avatars;

public sealed class RoomAvatarFactory(IServiceProvider host) : IRoomAvatarFactory
{
    private readonly IServiceProvider _host = host;

    public IRoomPlayerAvatar CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    )
    {
        var avatar = new RoomPlayerAvatar
        {
            Name = snapshot.Name,
            Motto = snapshot.Motto,
            Figure = snapshot.Figure,
            ObjectId = objectId,
            X = 5,
            Y = 5,
            Z = 0.0,
            Rotation = Rotation.North,
            PlayerId = snapshot.PlayerId,
            Gender = snapshot.Gender,
        };

        return avatar;
    }
}
