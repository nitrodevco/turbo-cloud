using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
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
    public Altitude Height { get; }
    public IExtraData ExtraData { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public bool IsInvisible { get; }

    /// <summary>
    /// A furni that exists only in this room while it is loaded (wired placed it): it has no
    /// database row and no place in an inventory, and its id is negative so it can never be
    /// mistaken for one that has.
    /// </summary>
    public bool IsTemporary { get; }

    /// <summary>
    /// A furni the Builders Club lends: it has a row and outlives the room, but nobody owns it,
    /// it can never reach an inventory, and picking it up destroys it. Its id is in the band the
    /// client reads that out of (<see cref="Furniture.FurniIdBands"/>).
    /// </summary>
    public bool IsBuildersClub { get; }

    /// <summary>Which of the three kinds this is, as the wired <c>@type</c> variable reports it.</summary>
    public FurnitureOwnershipType Ownership { get; }
    public void SetExtraData(string? extraData);
    public void SetOwnerId(PlayerId ownerId);
    public void SetOwnerName(string ownerName);
    public Altitude GetStackHeight();
    public RoomItemSnapshot GetSnapshot();
    public IComposer GetAddComposer();
    public IComposer GetUpdateComposer();
    public IComposer GetRefreshStuffDataComposer();
    public IComposer GetRemoveComposer(PlayerId pickerId, bool isExpired = false, int delay = 0);
}
