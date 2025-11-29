using System.Collections.Generic;
using Turbo.Primitives.Rooms.Object.Furniture.StuffData;

namespace Turbo.Rooms.Object.Furniture.StuffData;

internal sealed class StringStuffData : StuffDataBase, IStringStuffData
{
    public List<string> Data { get; set; } = [];

    public StringStuffData()
    {
        if (Data.Count == 0)
            Data.Add(DEFAULT_STATE);
    }

    public override string GetLegacyString() => GetValue(STATE_INDEX);

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

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
