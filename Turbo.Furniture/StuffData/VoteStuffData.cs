using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class VoteStuffData : StuffDataBase, IVoteStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.VoteKey;

    public string Data { get; set; } = DEFAULT_STATE;
    public int Result { get; set; } = 0;

    public override string GetLegacyString() => Data;

    public override Task SetStateAsync(string state)
    {
        if (string.IsNullOrEmpty(state))
            state = DEFAULT_STATE;

        Data = state;

        MarkDirty();

        return Task.CompletedTask;
    }

    public int GetResult() => Result;

    public void SetResult(int result)
    {
        Result = result;

        MarkDirty();
    }

    protected override StuffDataSnapshot BuildSnapshot() =>
        new VoteStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
            Data = GetLegacyString(),
            Result = GetResult(),
        };
}
