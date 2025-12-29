using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Avatars;

internal class RoomAvatarContext(
    RoomGrain roomGrain,
    RoomAvatarModule avatarModule,
    IRoomAvatar roomAvatar
) : RoomObjectContext(roomGrain), IRoomAvatarContext
{
    protected readonly RoomAvatarModule _avatarModule = avatarModule;

    public override RoomObjectId ObjectId => Avatar.ObjectId;

    public IRoomAvatar Avatar => roomAvatar;

    public virtual Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _room.GetTileSnapshotAsync(Avatar.X, Avatar.Y, ct);
}
