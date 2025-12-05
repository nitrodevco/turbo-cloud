using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Factories;

public interface IRoomAvatarFactory
{
    public IRoomPlayerAvatar CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    );
}
