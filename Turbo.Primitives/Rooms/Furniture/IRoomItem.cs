using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
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

    public void SetLogic(TLogic logic);
    public void SetStuffData(IStuffData stuffData);
}
