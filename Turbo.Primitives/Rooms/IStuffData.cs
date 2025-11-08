using Turbo.Contracts.Enums.Rooms.Furniture.Data;

namespace Turbo.Primitives.Rooms;

public interface IStuffData
{
    public StuffDataTypeEnum Type { get; }
    public int UniqueNumber { get; }
    public int UniqueSeries { get; }
    public string GetLegacyString();
    public void SetState(string state);
    public int GetState();
    public bool IsUnique();
}
