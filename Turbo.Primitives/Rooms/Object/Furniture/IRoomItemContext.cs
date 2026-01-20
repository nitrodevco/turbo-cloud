using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItemContext<out TObject, out TLogic, out TSelf>
    : IRoomObjectContext<TObject, TLogic, TSelf>,
        IRoomItemContext
    where TObject : IRoomItem<TObject, TLogic, TSelf>
    where TSelf : IRoomItemContext<TObject, TLogic, TSelf>
    where TLogic : IFurnitureLogic<TObject, TLogic, TSelf>
{
    new TObject Object { get; }
}

public interface IRoomItemContext : IRoomObjectContext
{
    new IRoomItem Object { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public Task<RoomFloorItemSnapshot?> GetFloorItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    );
    public Task PublishRoomEventAsync(RoomEvent evt, CancellationToken ct);
    public Task SendComposerToRoomAsync(IComposer composer);
    public Task AddItemAsync();
    public Task UpdateItemAsync();
    public Task RefreshStuffDataAsync();
    public Task RemoveItemAsync(PlayerId pickerId, bool isExpired = false, int delay = 0);
}
