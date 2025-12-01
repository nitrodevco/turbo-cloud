using Turbo.Primitives.Orleans.Snapshots.Players;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomAvatarFactory
{
    public IRoomPlayerAvatar CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    );
}
