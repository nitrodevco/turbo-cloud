using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class MapStuffData : StuffDataBase, IMapStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.MapKey;

    public Dictionary<string, string> Data { get; set; } = [];

    public MapStuffData()
    {
        Data.TryAdd(STATE_KEY, DEFAULT_STATE);
    }

    public override string GetLegacyString() => GetValue(STATE_KEY);

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data[STATE_KEY] = state;

        MarkDirty();
    }

    public string GetValue(string key)
    {
        if (Data.TryGetValue(key, out var value))
            return value;

        return string.Empty;
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new MapStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = Data.ToImmutableDictionary(),
        };
}
