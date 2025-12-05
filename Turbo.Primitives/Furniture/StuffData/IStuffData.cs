using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.Furniture.StuffData;

public interface IStuffData
{
    public StuffDataType StuffType { get; }
    public int UniqueNumber { get; }
    public int UniqueSeries { get; }
    public bool IsDirty { get; }
    public int GetBitmask();
    public void SetType(StuffDataType type);
    public bool IsUnique();
    public int GetState();
    public void SetState(string state);
    public string GetLegacyString();
    public void SetAction(System.Action? onSnapshotChanged);
    public void MarkDirty();
    public StuffDataSnapshot GetSnapshot();
}
