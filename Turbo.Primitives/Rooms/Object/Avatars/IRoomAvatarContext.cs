using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Primitives.Rooms.Object.Avatars;

public interface IRoomAvatarContext<out TObject, out TLogic, out TSelf>
    : IRoomObjectContext<TObject, TLogic, TSelf>,
        IRoomAvatarContext
    where TObject : IRoomAvatar<TObject, TLogic, TSelf>
    where TSelf : IRoomAvatarContext<TObject, TLogic, TSelf>
    where TLogic : IRoomAvatarLogic<TObject, TLogic, TSelf>
{
    new TObject RoomObject { get; }
}

public interface IRoomAvatarContext : IRoomObjectContext
{
    new IRoomAvatar Object { get; }
    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct);
}
