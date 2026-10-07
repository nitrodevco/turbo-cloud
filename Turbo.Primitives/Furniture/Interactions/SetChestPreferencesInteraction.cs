using Orleans;
using Turbo.Primitives.WiredTrading.Enums;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>A wired chest's settings window was saved.</summary>
[GenerateSerializer, Immutable]
public sealed record SetChestPreferencesInteraction : FurnitureInteraction
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required string Description { get; init; }

    [Id(2)]
    public required bool EveryoneCanOpen { get; init; }

    [Id(3)]
    public required bool EveryoneCanDonate { get; init; }

    [Id(4)]
    public required WiredChestStateMode StateMode { get; init; }

    [Id(5)]
    public required WiredChestPreviewMode PreviewMode { get; init; }

    [Id(6)]
    public required int PreviewAmount { get; init; }

    /// <summary>True asks to make it a wired chest, which cannot be undone.</summary>
    [Id(7)]
    public required bool WiredEnabled { get; init; }
}
