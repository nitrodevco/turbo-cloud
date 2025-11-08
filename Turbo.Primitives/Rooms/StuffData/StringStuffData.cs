using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.StuffData;

public sealed class StringStuffData : StuffDataBase
{
    private const int STATE_INDEX = 0;

    public List<string> Data { get; private set; } = [];

    public override string GetLegacyString() => GetValue(STATE_INDEX);

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = "0";

        Data[STATE_INDEX] = state;
    }

    public override object GetJsonData()
    {
        var parent = base.GetJsonData();

        return new { parent, Data };
    }

    public string GetValue(int index)
    {
        if (index < 0 || index >= Data.Count)
            return string.Empty;

        return Data[index];
    }
}
