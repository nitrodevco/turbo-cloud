using Orleans;

namespace Turbo.Primitives.Navigator.Snapshots;

[GenerateSerializer, Immutable]
public sealed record NavigatorPopularTagSnapshot
{
    [Id(0)]
    public required string Tag { get; init; }

    [Id(1)]
    public required int UserCount { get; init; }
}
