using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class StringStuffData : StuffDataBase, IStringStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.StringKey;

    public List<string> Data { get; set; } = [];

    public StringStuffData()
    {
        if (Data.Count == 0)
            Data.Add(DEFAULT_STATE);
    }

    public override string GetLegacyString() => GetValue(STATE_INDEX);

    public override Task SetStateAsync(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data[STATE_INDEX] = state;

        MarkDirty();

        return Task.CompletedTask;
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

        MarkDirty();
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new StringStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = [.. Data],
        };
}
