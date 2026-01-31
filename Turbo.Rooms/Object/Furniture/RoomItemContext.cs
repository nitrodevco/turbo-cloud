using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Furniture;

public abstract class RoomItemContext<TObject, TLogic, TSelf>(
    RoomGrain roomGrain,
    TObject roomObject
)
    : RoomObjectContext<TObject, TLogic, TSelf>(roomGrain, roomObject),
        IRoomItemContext<TObject, TLogic, TSelf>
    where TObject : IRoomItem<TObject, TLogic, TSelf>
    where TSelf : IRoomItemContext<TObject, TLogic, TSelf>
    where TLogic : IFurnitureLogic<TObject, TLogic, TSelf>
{
    public FurnitureDefinitionSnapshot Definition => _roomObject.Definition;

    IRoomItem IRoomItemContext.RoomObject => RoomObject;

    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) => _roomGrain.GetFloorItemSnapshotByIdAsync(objectId, ct);

    public virtual Task AddItemAsync() => SendComposerToRoomAsync(RoomObject.GetAddComposer());

    public virtual Task UpdateItemAsync() =>
        SendComposerToRoomAsync(RoomObject.GetUpdateComposer());

    public virtual Task RefreshStuffDataAsync() =>
        SendComposerToRoomAsync(RoomObject.GetRefreshStuffDataComposer());

    public virtual Task RemoveItemAsync(PlayerId pickerId, bool isExpired = false, int delay = 0) =>
        SendComposerToRoomAsync(RoomObject.GetRemoveComposer(pickerId));
}
