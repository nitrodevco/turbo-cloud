using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

[GenerateSerializer, Immutable]
public sealed record AreaHideDataSnapshot
{
    [Id(0)]
    public required int FurniId { get; init; }

    [Id(1)]
    public required bool On { get; init; }

    [Id(2)]
    public required int RootX { get; init; }

    [Id(3)]
    public required int RootY { get; init; }

    [Id(4)]
    public required int Width { get; init; }

    [Id(5)]
    public required int Length { get; init; }

    [Id(6)]
    public required bool Invert { get; init; }
}
