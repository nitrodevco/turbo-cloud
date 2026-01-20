using Turbo.Furniture;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Logic.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Object.Furniture;

public abstract class RoomItem<TSelf, TLogic, TContext>
    : RoomObject<TSelf, TLogic, TContext>,
        IRoomItem<TSelf, TLogic, TContext>
    where TSelf : IRoomItem<TSelf, TLogic, TContext>
    where TContext : IRoomItemContext<TSelf, TLogic, TContext>
    where TLogic : IFurnitureLogic<TSelf, TLogic, TContext>
{
    public required PlayerId OwnerId { get; set; }
    public required string OwnerName { get; set; } = string.Empty;
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    IFurnitureLogic IRoomItem.Logic => Logic;

    private IExtraData _extraData = null!;
    private RoomItemSnapshot? _snapshot;

    public IExtraData ExtraData => _extraData;

    public void SetExtraData(string? extraData)
    {
        _extraData = new ExtraData(extraData);

        _extraData.SetAction(async () => MarkDirty());
    }

    public void SetOwnerId(PlayerId ownerId)
    {
        OwnerId = ownerId;

        MarkDirty();
    }

    public void SetOwnerName(string ownerName)
    {
        OwnerName = ownerName;
    }

    public RoomItemSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    protected abstract RoomItemSnapshot BuildSnapshot();

    public abstract IComposer GetAddComposer();

    public abstract IComposer GetUpdateComposer();

    public abstract IComposer GetRefreshStuffDataComposer();

    public abstract IComposer GetRemoveComposer(
        PlayerId pickerId,
        bool isExpired = false,
        int delay = 0
    );
}
