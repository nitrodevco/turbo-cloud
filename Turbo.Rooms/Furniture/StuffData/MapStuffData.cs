using System.Collections.Generic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class MapStuffData : StuffDataBase, IMapStuffData
{
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
    }

    public string GetValue(string key)
    {
        if (Data.TryGetValue(key, out var value))
            return value;

        return string.Empty;
    }
}
