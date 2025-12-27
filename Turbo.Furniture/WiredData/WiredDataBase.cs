using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.WiredData;
using Turbo.Primitives.Furniture.WiredData;

namespace Turbo.Furniture.WiredData;

internal abstract class WiredDataBase : IWiredData
{
    [JsonIgnore]
    public int WiredCode { get; set; }

    [JsonIgnore]
    public int FurniLimit { get; set; }

    public List<int> StuffIds { get; set; } = [];

    public List<int> IntParams { get; set; } = [];

    public List<int> VariableIds { get; set; } = [];

    public string StringParam { get; set; } = string.Empty;
    public List<int> FurniSources { get; set; } = [];
    public List<int> PlayerSources { get; set; } = [];

    private WiredDataSnapshot? _snapshot;
    private Func<Task>? _onSnapshotChanged;
    private bool _dirty = true;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public void MarkDirty()
    {
        _dirty = true;

        _ = _onSnapshotChanged?.Invoke();
    }

    public virtual WiredDataSnapshot GetSnapshot()
    {
        if (_dirty || _snapshot is null)
        {
            _snapshot = BuildSnapshot();
            _dirty = false;
        }

        return _snapshot;
    }

    protected abstract WiredDataSnapshot BuildSnapshot();
}
