using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Object.Avatars;

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
