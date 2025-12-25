using System;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.Furniture.StuffData;

public interface IStuffData
{
    public StuffDataType StuffType { get; }
    public int UniqueNumber { get; }
    public int UniqueSeries { get; }
    public bool IsDirty { get; }
    public int GetBitmask();
    public bool IsUnique();
    public int GetState();
    public Task SetStateAsync(string state);
    public string GetLegacyString();
    public void SetAction(Func<Task>? onSnapshotChanged);
    public void MarkDirty();
    public StuffDataSnapshot GetSnapshot();
    public string ToJson();
}
