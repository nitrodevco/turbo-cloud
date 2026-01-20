using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Object.Avatars.Player;

namespace Turbo.Rooms.Providers;

public sealed class RoomAvatarProvider : IRoomAvatarProvider
{
    public IRoomPlayer CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    ) =>
        new RoomPlayerAvatar
        {
            Name = snapshot.Name,
            Motto = snapshot.Motto,
            Figure = snapshot.Figure,
            ObjectId = objectId,
            PlayerId = snapshot.PlayerId,
            Gender = snapshot.Gender,
        };
}
