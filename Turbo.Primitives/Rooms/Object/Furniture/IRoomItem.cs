using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Rooms.Object.Furniture;

public interface IRoomItem : IRoomObject
{
    public PlayerId OwnerId { get; }
    public string OwnerName { get; }
    public string PendingStuffDataRaw { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public void SetOwnerId(PlayerId ownerId);
    public void SetOwnerName(string ownerName);
}
