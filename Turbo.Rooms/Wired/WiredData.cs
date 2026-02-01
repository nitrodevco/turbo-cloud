using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

public class WiredData : IWiredData
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

    private IReadOnlyList<IWiredIntParamRule> _rules = [];

    private Func<Task>? _onSnapshotChanged;

    public T GetIntParam<T>(int index)
    {
        var rule = _rules[index];

        if (rule.ValueType != typeof(T))
            throw new InvalidOperationException(
                $"Param {index} is {rule.ValueType?.Name}, not {typeof(T).Name}"
            );

        return (T)rule.FromInt(IntParams[index]);
    }

    public void SetIntParam<T>(int index, T value)
    {
        var rule = _rules[index];

        if (rule.ValueType != typeof(T))
            throw new InvalidOperationException(
                $"Param {index} is {rule.ValueType?.Name}, not {typeof(T).Name}"
            );

        IntParams[index] = rule.Sanitize(rule.ToInt(value!));

        MarkDirty();
    }

    public void AttatchRules(IReadOnlyList<IWiredIntParamRule> rules) => _rules = rules;

    public void SetAction(Func<Task>? onSnapshotChanged) => _onSnapshotChanged = onSnapshotChanged;

    public void MarkDirty()
    {
        _ = _onSnapshotChanged?.Invoke();
    }
}
