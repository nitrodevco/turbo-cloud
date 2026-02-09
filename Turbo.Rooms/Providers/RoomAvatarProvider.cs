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
    )
    {
        var avatar = new RoomPlayerAvatar { ObjectId = objectId, PlayerId = snapshot.PlayerId };

        avatar.UpdateWithPlayer(snapshot);

        return avatar;
    }
}
