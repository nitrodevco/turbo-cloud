using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Furniture;

[GenerateSerializer, Immutable]
public sealed record DimmerPresetSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int Type { get; init; }

    [Id(2)]
    public required string Color { get; init; }

    [Id(3)]
    public required int Brightness { get; init; }
}
