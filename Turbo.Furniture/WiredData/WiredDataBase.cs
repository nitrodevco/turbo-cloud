using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.WiredData;

namespace Turbo.Furniture.WiredData;

internal abstract class WiredDataBase : IWiredData
{
    public List<int> IntParams { get; set; } = [];

    public List<int> VariableIds { get; set; } = [];

    public string StringParam { get; set; } = string.Empty;

    public List<int> StuffIds { get; set; } = [];
    public List<int> FurniSources { get; set; } = [];
    public List<int> PlayerSources { get; set; } = [];

    protected Func<Task>? _onSnapshotChanged;
    protected bool _dirty = true;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public void MarkDirty()
    {
        _dirty = true;

        _ = _onSnapshotChanged?.Invoke();
    }
}
