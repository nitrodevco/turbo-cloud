using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Primitives.Furniture.WiredData;

public interface IWiredData
{
    public int Id { get; set; }
    public List<int> IntParams { get; set; }
    public string StringParam { get; set; }
    public List<int> StuffIds { get; set; }
    public List<int> StuffIds2 { get; set; }
    public List<string> VariableIds { get; set; }
    public List<WiredFurniSourceType[]> FurniSources { get; set; }
    public List<WiredPlayerSourceType[]> PlayerSources { get; set; }
    public List<object> DefinitionSpecifics { get; set; }
    public List<object> TypeSpecifics { get; set; }
    public T GetIntParam<T>(int index);
    public void SetIntParam<T>(int index, T value);
    public void AttatchRules(IReadOnlyList<IWiredIntParamRule> rules);
    public void SetAction(Func<Task>? onSnapshotChanged);
    public void MarkDirty();
}
