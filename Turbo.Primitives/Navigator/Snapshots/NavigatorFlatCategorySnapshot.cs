using Orleans;

namespace Turbo.Primitives.Navigator.Snapshots;

[GenerateSerializer, Immutable]
public sealed record NavigatorFlatCategorySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required bool Visible { get; init; }

    [Id(3)]
    public required bool Automatic { get; init; }

    [Id(4)]
    public required string AutomaticCategoryKey { get; init; }

    [Id(5)]
    public required string GlobalCategoryKey { get; init; }

    [Id(6)]
    public required bool StaffOnly { get; init; }

    [Id(7)]
    public required int MinRank { get; init; }

    [Id(8)]
    public required int OrderNum { get; init; }
}
