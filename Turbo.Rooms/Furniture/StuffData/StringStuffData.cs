using System.Collections.Generic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class StringStuffData : StuffDataBase, IStringStuffData
{
    private const int STATE_INDEX = 0;

    public List<string> Data { get; set; } = [];

    public override string GetLegacyString() => GetValue(STATE_INDEX);

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data[STATE_INDEX] = state;
    }

    public string GetValue(int index)
    {
        if (index < 0 || index >= Data.Count)
            return string.Empty;

        return Data[index];
    }

    public void SetValue(int index, string value)
    {
        Data[index] = value;
    }
}
