using System.Threading.Tasks;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Furniture.StuffData;

internal sealed class EmptyStuffData : StuffDataBase, IEmptyStuffData
{
    public override string GetLegacyString() => string.Empty;

    public override Task SetStateAsync(string state) => Task.CompletedTask;

    protected override StuffDataSnapshot BuildSnapshot() =>
        new EmptyStuffSnapshot()
        {
            StuffBitmask = GetBitmask(),
            UniqueNumber = UniqueNumber,
            UniqueSeries = UniqueSeries,
        };
}
