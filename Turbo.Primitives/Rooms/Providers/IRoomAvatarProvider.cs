using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Rooms.Providers;

public interface IRoomAvatarProvider
{
    public IRoomPlayer CreateAvatarFromPlayerSnapshot(
        RoomObjectId objectId,
        PlayerSummarySnapshot snapshot
    );
}
