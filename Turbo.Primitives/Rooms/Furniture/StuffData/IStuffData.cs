namespace Turbo.Primitives.Rooms.Furniture.StuffData;

public interface IStuffData
{
    public StuffDataType StuffType { get; }
    public int UniqueNumber { get; }
    public int UniqueSeries { get; }
    public int GetBitmask();
    public void SetType(StuffDataType type);
    public bool IsUnique();
    public int GetState();
    public void SetState(string state);
    public string GetLegacyString();
}
