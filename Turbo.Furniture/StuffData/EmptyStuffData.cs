using System.Text.Json.Serialization;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class EmptyStuffData : StuffDataBase, IEmptyStuffData
{
    [JsonIgnore]
    public override StuffDataType StuffType => StuffDataType.EmptyKey;

    public override string GetLegacyString() => string.Empty;

    protected override StuffDataSnapshot BuildSnapshot() =>
        new EmptyStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
        };
}
