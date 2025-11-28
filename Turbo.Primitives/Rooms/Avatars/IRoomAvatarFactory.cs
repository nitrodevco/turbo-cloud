using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Avatars;

public interface IRoomAvatarFactory
{
    public IRoomPlayerAvatar CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    );
}
