using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.Logic;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Rooms.Furniture;

public class RoomItem<TLogic> : IRoomItem<TLogic>
    where TLogic : IFurnitureLogic
{
    public required long Id { get; init; }
    public required long OwnerId { get; init; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public required string StuffDataRaw { get; init; }
    public TLogic Logic { get; private set; } = default!;
    public IStuffData StuffData { get; private set; } = default!;

    public void SetLogic(TLogic logic)
    {
        Logic = logic;

        Logic.SetupStuffDataFromJson(StuffDataRaw);
    }

    public void SetStuffData(IStuffData stuffData)
    {
        StuffData = stuffData;
    }
}
