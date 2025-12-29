using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Modules;

namespace Turbo.Rooms.Object.Furniture.Floor;

internal sealed class RoomFloorItemContext(
    RoomGrain roomGrain,
    RoomFurniModule furniModule,
    IRoomFloorItem roomItem
) : RoomItemContext<IRoomFloorItem>(roomGrain, furniModule, roomItem), IRoomFloorItemContext
{
    public int GetTileIdx() => _room.ToIdx(Item.X, Item.Y);

    public int GetTileIdx(int x, int y) => _room.ToIdx(x, y);

    public override Task AddItemAsync() => SendComposerToRoomAsync(Item.GetAddComposer());

    public override Task UpdateItemAsync() => SendComposerToRoomAsync(Item.GetUpdateComposer());

    public override Task RefreshStuffDataAsync() =>
        SendComposerToRoomAsync(Item.GetRefreshStuffDataComposer());

    public override Task RemoveItemAsync(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    ) => SendComposerToRoomAsync(Item.GetRemoveComposer(pickerId, isExpired, delay));

    public void RefreshTile() => _room.ComputeTile(Item.X, Item.Y);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(CancellationToken ct) =>
        _room.GetTileSnapshotAsync(Item.X, Item.Y, ct);
}
