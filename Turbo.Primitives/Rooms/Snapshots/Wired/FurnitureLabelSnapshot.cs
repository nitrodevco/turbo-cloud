using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// One entry of <c>FurnitureLabelsAndRegions</c> / <c>FurnitureLabelsAndRegionsUpdate</c>: the
/// furni it belongs to and its text (a region's title), then either the label's bubble colour,
/// audience and "always open" or, with <see cref="Region"/> set, the region's own fields.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureLabelSnapshot
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required string Text { get; init; }

    [Id(2)]
    public int BubbleColor { get; init; }

    [Id(3)]
    public FurnitureLabelVisibilityType Visibility { get; init; }

    [Id(4)]
    public bool AlwaysOpen { get; init; }

    [Id(5)]
    public FurnitureRegionSnapshot? Region { get; init; }

    public FurnitureLabelType Type =>
        Region is null ? FurnitureLabelType.Label : FurnitureLabelType.Region;
}
