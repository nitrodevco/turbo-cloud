using System.Globalization;
using System.Text.Json.Serialization;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class CrackableStuffData : StuffDataBase, ICrackableStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.CrackableKey;

    public string Data { get; set; } = DEFAULT_STATE;
    public int Hits { get; set; } = 0;
    public int Target { get; set; } = 0;

    public override string GetLegacyString() => Data;

    public override void SetState(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data = state;

        MarkDirty();
    }

    public void SetProgress(int hits, int target, int state)
    {
        Hits = hits;
        Target = target;
        Data = state.ToString(CultureInfo.InvariantCulture);

        MarkDirty();
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new CrackableStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = GetLegacyString(),
            Hits = Hits,
            Target = Target,
        };
}
