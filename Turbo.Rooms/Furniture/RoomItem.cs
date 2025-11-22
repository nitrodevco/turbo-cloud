using System.Text.Json;
using Turbo.Primitives.Rooms.Furniture;
using Turbo.Primitives.Rooms.Furniture.StuffData;
using Turbo.Primitives.Snapshots.Furniture;

namespace Turbo.Rooms.Furniture;

public abstract class RoomItem : IRoomItem
{
    public required long Id { get; init; }
    public required long OwnerId { get; init; }
    public required FurnitureDefinitionSnapshot Definition { get; init; }
    public IStuffData StuffData { get; private set; } = default!;
    private string _stuffDataRaw = string.Empty;

    public virtual string GetStuffDataRaw()
    {
        if (StuffData is null)
            return _stuffDataRaw;

        return JsonSerializer.Serialize(StuffData);
    }

    public virtual void SetStuffDataRaw(string stuffDataRaw) => _stuffDataRaw = stuffDataRaw;

    public void SetStuffData(IStuffData stuffData) => StuffData = stuffData;
}
