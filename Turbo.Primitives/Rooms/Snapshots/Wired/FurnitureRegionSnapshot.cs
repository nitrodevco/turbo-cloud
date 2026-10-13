using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// The fields only a region entry carries after its title (Flash <c>FurnitureRegionData</c>, in
/// the order <c>_-MK.parse</c> reads them).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureRegionSnapshot
{
    [Id(0)]
    public required string Description { get; init; }

    [Id(1)]
    public required int XOffset { get; init; }

    [Id(2)]
    public required int YOffset { get; init; }

    [Id(3)]
    public required int MinWidth { get; init; }

    [Id(4)]
    public required int MinHeight { get; init; }

    /// <summary>A string, unlike a label's colour.</summary>
    [Id(5)]
    public required string Color { get; init; }

    [Id(6)]
    public required ImmutableArray<FurnitureRegionAreaSnapshot> Areas { get; init; }
}
