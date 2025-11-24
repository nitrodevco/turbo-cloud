using System.Collections.Generic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class MapStuffData : StuffDataBase, IMapStuffData
{
    private const string STATE_KEY = "state";

    public Dictionary<string, string> Data { get; private set; } = [];

    public override string GetLegacyString() => GetValue(STATE_KEY);

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data.Remove(STATE_KEY);
        Data.Add(STATE_KEY, state);
    }

    public string GetValue(string key)
    {
        if (Data.TryGetValue(key, out var value))
            return value;

        return string.Empty;
    }
}
