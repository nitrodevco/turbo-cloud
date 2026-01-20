using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItem<TSelf, out TLogic, out TContext>
    : IRoomObject<TSelf, TLogic, TContext>,
        IRoomItem
    where TSelf : IRoomItem<TSelf, TLogic, TContext>
    where TContext : IRoomItemContext<TSelf, TLogic, TContext>
    where TLogic : IFurnitureLogic<TSelf, TLogic, TContext>
{
    new TLogic Logic { get; }
}

public interface IRoomItem : IRoomObject
{
    new IFurnitureLogic Logic { get; }
    public PlayerId OwnerId { get; }
    public string OwnerName { get; }
    public IExtraData ExtraData { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public void SetExtraData(string? extraData);
    public void SetOwnerId(PlayerId ownerId);
    public void SetOwnerName(string ownerName);
    public RoomItemSnapshot GetSnapshot();
    public IComposer GetAddComposer();
    public IComposer GetUpdateComposer();
    public IComposer GetRefreshStuffDataComposer();
    public IComposer GetRemoveComposer(PlayerId pickerId, bool isExpired = false, int delay = 0);
}
