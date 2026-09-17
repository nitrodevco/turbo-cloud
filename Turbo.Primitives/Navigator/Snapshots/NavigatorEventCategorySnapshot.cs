using Orleans;

namespace Turbo.Primitives.Navigator.Snapshots;

[GenerateSerializer, Immutable]
public sealed record NavigatorEventCategorySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required bool Visible { get; init; }
}
