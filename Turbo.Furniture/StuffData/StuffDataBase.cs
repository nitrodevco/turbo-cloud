using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal abstract class StuffDataBase : IStuffData
{
    public const int TYPE_MASK = 0xFF;
    public const int FLAGS_MASK = 0xFF00;
    protected const string DEFAULT_STATE = "0";
    protected const int STATE_INDEX = 0;
    protected const string STATE_KEY = "state";

    [JsonIgnore]
    public abstract StuffDataType StuffType { get; }

    [JsonPropertyName("U_N")]
    public int UniqueNumber { get; set; } = 0;

    [JsonPropertyName("U_S")]
    public int UniqueSeries { get; set; } = 0;

    [JsonIgnore]
    public bool IsDirty => _dirty;

    protected Func<Task>? _onSnapshotChanged;
    protected bool _dirty = true;
    protected StuffDataSnapshot? _snapshot;

    public static int CreateBitmask(StuffDataType type, StuffDataFlags flags) =>
        ((int)type & TYPE_MASK) | ((int)flags & FLAGS_MASK);

    public int GetBitmask() =>
        CreateBitmask(StuffType, IsUnique() ? StuffDataFlags.Unique : StuffDataFlags.None);

    public bool IsUnique() => UniqueNumber > 0 && UniqueSeries > 0;

    public virtual int GetState() => int.Parse(GetLegacyString());

    public abstract Task SetStateAsync(string state);

    public virtual string GetLegacyString() => string.Empty;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public void MarkDirty()
    {
        _dirty = true;

        _ = _onSnapshotChanged?.Invoke();
    }

    public virtual StuffDataSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    public virtual string ToJson() => JsonSerializer.Serialize(this, GetType());

    protected abstract StuffDataSnapshot BuildSnapshot();
}
