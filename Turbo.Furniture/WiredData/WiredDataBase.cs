using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Furniture.WiredData;

internal abstract class WiredDataBase : IWiredData
{
    public List<int> StuffIds { get; set; } = [];

    public List<int> IntParams { get; set; } = [];

    public List<long> VariableIds { get; set; } = [];

    public string StringParam { get; set; } = string.Empty;
    public Dictionary<int, WiredSourceType> FurniSources { get; set; } = [];
    public Dictionary<int, WiredSourceType> PlayerSources { get; set; } = [];

    private Func<Task>? _onSnapshotChanged;
    private bool _dirty = true;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public void MarkDirty()
    {
        _dirty = true;

        _ = _onSnapshotChanged?.Invoke();
    }
}
