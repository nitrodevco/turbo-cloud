using System.Collections.Generic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class NumberStuffData : StuffDataBase, INumberStuffData
{
    public List<int> Data { get; set; } = [];

    public NumberStuffData()
    {
        if (Data.Count == 0)
            Data.Add(int.Parse(DEFAULT_STATE));
    }

    public override string GetLegacyString() => GetValue(STATE_INDEX).ToString();

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data[STATE_INDEX] = int.Parse(state);
    }

    public int GetValue(int index)
    {
        if (index < 0 || index >= Data.Count)
            return 0;

        return Data[index];
    }

    public void SetValue(int index, int value)
    {
        if (index < 0)
            return;

        Data[index] = value;
    }
}
