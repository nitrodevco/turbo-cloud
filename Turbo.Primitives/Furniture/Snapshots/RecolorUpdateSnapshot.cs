using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Furniture.Snapshots;

/// <summary>Furni that now share one colour, as one entry of <c>FurniRecolorUpdate</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record RecolorUpdateSnapshot
{
    [Id(0)]
    public required ImmutableArray<RoomObjectId> ObjectIds { get; init; }

    [Id(1)]
    public required int Red { get; init; }

    [Id(2)]
    public required int Green { get; init; }

    [Id(3)]
    public required int Blue { get; init; }
}
