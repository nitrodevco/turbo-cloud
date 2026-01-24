using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Avatars;

public abstract class RoomAvatarContext<TObject, TLogic, TSelf>(
    RoomGrain roomGrain,
    TObject roomObject
)
    : RoomObjectContext<TObject, TLogic, TSelf>(roomGrain, roomObject),
        IRoomAvatarContext<TObject, TLogic, TSelf>
    where TObject : IRoomAvatar<TObject, TLogic, TSelf>
    where TSelf : IRoomAvatarContext<TObject, TLogic, TSelf>
    where TLogic : IRoomAvatarLogic<TObject, TLogic, TSelf>
{
    IRoomAvatar IRoomAvatarContext.RoomObject => RoomObject;

    public virtual Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _roomGrain.GetTileSnapshotAsync(RoomObject.X, RoomObject.Y, ct);
}
