using System.Collections.Generic;
using System.Text.Json.Serialization;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class NumberStuffData : StuffDataBase, INumberStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.NumberKey;

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

        MarkDirty();
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

        MarkDirty();
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new NumberStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = [.. Data],
        };
}
