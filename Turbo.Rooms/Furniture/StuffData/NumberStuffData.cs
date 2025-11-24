using System.Collections.Generic;
using Turbo.Primitives.Rooms.Furniture.StuffData;

namespace Turbo.Rooms.Furniture.StuffData;

internal sealed class NumberStuffData : StuffDataBase, INumberStuffData
{
    private const int STATE_INDEX = 0;

    public List<int> Data { get; private set; } = [];

    public override string GetLegacyString() => GetValue(STATE_INDEX).ToString();

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data[STATE_INDEX] = int.Parse(state);
    }

    public int GetValue(int index)
    {
        if (index < 0 || index >= Data.Count)
            return 0;

        return Data[index];
    }
}
