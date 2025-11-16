using Turbo.Primitives.Rooms.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Primitives.Rooms.Furniture;

public interface IRoomItem<TLogic>
    where TLogic : IFurnitureLogic
{
    public long Id { get; }
    public long OwnerId { get; }
    public FurnitureDefinitionSnapshot Definition { get; }
    public TLogic Logic { get; }
    public IStuffData StuffData { get; }
}
