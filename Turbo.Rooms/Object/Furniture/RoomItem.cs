using Turbo.Furniture;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object.Furniture;

namespace Turbo.Rooms.Object.Furniture;

internal abstract class RoomItem : RoomObject, IRoomItem
{
    public required PlayerId OwnerId { get; set; }
    public required string OwnerName { get; set; } = string.Empty;
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    private IExtraData _extraData = null!;

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
}
