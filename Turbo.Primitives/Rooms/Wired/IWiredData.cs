using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

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
    public T GetDefinitionParam<T>(int index);
    public void SetDefinitionParam<T>(int index, T value);
    public T GetTypeParam<T>(int index);
    public void SetTypeParam<T>(int index, T value);

    /// <summary>
    /// The rules the int params are read by: one per fixed param, and <paramref name="tailRule"/>
    /// for every param after them (a box with a variable number of params).
    /// </summary>
    public void AttatchRules(
        IReadOnlyList<IWiredParamRule> rules,
        IWiredParamRule? tailRule = null
    );
    public void SetAction(Func<Task>? onSnapshotChanged);
    public void MarkDirty();
}
