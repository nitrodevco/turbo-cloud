using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Furniture.WiredData;

internal abstract class WiredDataBase : IWiredData
{
    [JsonIgnore]
    public int Id { get; set; }
    public List<int> IntParams { get; set; } = [];
    public string StringParam { get; set; } = string.Empty;
    public List<int> StuffIds { get; set; } = [];
    public List<int> StuffIds2 { get; set; } = [];
    public List<string> VariableIds { get; set; } = [];

    public List<WiredFurniSourceType[]> FurniSources { get; set; } = [];
    public List<WiredPlayerSourceType[]> PlayerSources { get; set; } = [];

    public List<object> DefinitionSpecifics { get; set; } = [];
    public List<object> TypeSpecifics { get; set; } = [];

    private Func<Task>? _onSnapshotChanged;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public void MarkDirty()
    {
        _ = _onSnapshotChanged?.Invoke();
    }
}
