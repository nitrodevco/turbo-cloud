using Turbo.Contracts.Enums.Rooms.Furniture.Data;

namespace Turbo.Primitives.Rooms.StuffData;

public interface IStuffData
{
    public StuffDataTypeEnum StuffType { get; }
    public int UniqueNumber { get; }
    public int UniqueSeries { get; }
    public void SetType(StuffDataTypeEnum type);
    public bool IsUnique();
    public int GetState();
    public void SetState(string state);
    public string GetLegacyString();
}
