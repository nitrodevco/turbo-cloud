using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Factories;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Avatars;

namespace Turbo.Rooms.Factories;

public sealed class RoomAvatarFactory : IRoomAvatarFactory
{
    public IRoomPlayerAvatar CreateAvatarFromPlayerSnapshot(
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
