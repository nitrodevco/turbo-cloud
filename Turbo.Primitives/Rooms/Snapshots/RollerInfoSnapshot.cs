using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RollerInfoSnapshot
{
    [Id(0)]
    public required long ItemId { get; init; }

    [Id(1)]
    public required int X { get; init; }

    [Id(2)]
    public required int Y { get; init; }

    [Id(3)]
    public required int TargetX { get; init; }

    [Id(4)]
    public required int TargetY { get; init; }

    //[Id(5)]
    //public required ImmutableList<(long, double, double)> Furniture { get; init; }
}
