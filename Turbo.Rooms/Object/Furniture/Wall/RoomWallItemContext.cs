using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Object.Furniture.Wall;

public sealed class RoomWallItemContext(RoomGrain roomGrain, IRoomWallItem roomObject)
    : RoomItemContext<IRoomWallItem, IFurnitureWallLogic, IRoomWallItemContext>(
        roomGrain,
        roomObject
    ),
        IRoomWallItemContext
{
    public override Task AddItemAsync() => SendComposerToRoomAsync(Object.GetAddComposer());

    public override Task UpdateItemAsync() => SendComposerToRoomAsync(Object.GetUpdateComposer());

    public override Task RefreshStuffDataAsync() =>
        SendComposerToRoomAsync(Object.GetRefreshStuffDataComposer());

    public override Task RemoveItemAsync(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    ) => SendComposerToRoomAsync(Object.GetRemoveComposer(pickerId));
}
