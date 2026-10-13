using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>A label's settings window saved (<c>SetFurnitureLabelSettings</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record SetFurnitureLabelSettingsInteraction : FurnitureInteraction
{
    [Id(0)]
    public required int BubbleColor { get; init; }

    [Id(1)]
    public required FurnitureLabelVisibilityType Visibility { get; init; }

    [Id(2)]
    public required bool AlwaysOpen { get; init; }
}
