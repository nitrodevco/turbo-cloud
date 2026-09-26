using System.Collections.Generic;
using System.Globalization;
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
            Data.Add(int.Parse(DEFAULT_STATE, CultureInfo.InvariantCulture));
    }

    public override string GetLegacyString() => GetValue(STATE_INDEX).ToString();

    public override void SetState(string state)
    {
        // The state comes from a client or a wired box; one that is not a number is the default,
        // as an empty one is, rather than an exception in the middle of a room turn.
        if (!int.TryParse(state, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            value = int.Parse(DEFAULT_STATE, CultureInfo.InvariantCulture);

        Data[STATE_INDEX] = value;

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
        if (index < 0 || index >= Data.Count)
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
