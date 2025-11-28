using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Rooms.Avatars;

public interface IRoomAvatar
{
    public RoomObjectId ObjectId { get; }

    public RoomAvatarSnapshot GetSnapshot();
}
