using System.Collections.Generic;

namespace Turbo.Assets.Furniture;

/// <summary>
/// What a furniture's asset file says of it, beyond what furnidata says: its states, the logic and
/// visualization the client gives it, its size and the ways it can face.
/// </summary>
public sealed record FurnitureAssetInfo
{
    /// <summary>The asset's own name (<c>rare_dragonlamp</c>).</summary>
    public required string Type { get; init; }

    /// <summary>The client's logic for it (<c>furniture_multistate</c>), from the index.</summary>
    public string? Logic { get; init; }

    /// <summary>The client's visualization for it (<c>furniture_animated</c>), from the index.</summary>
    public string? Visualization { get; init; }

    /// <summary>
    /// How many states it toggles through: one past the highest animation that is a state, so
    /// state 0 counts even when it has no animation of its own (a lamp: off, then animation 1).
    /// Habbo numbers states from 0; its transitions between states (from 100, or marked
    /// <c>transitionTo</c> / <c>transitionFrom</c>) and its special animations (below 0: a dice
    /// rolling) are not states. Zero when it has no state animations: it does not change when used.
    /// </summary>
    public required int States { get; init; }

    /// <summary>The ids of the animations that are states.</summary>
    public required IReadOnlyList<int> StateAnimations { get; init; }

    /// <summary>The ids of its other animations: transitions and special ones.</summary>
    public required IReadOnlyList<int> OtherAnimations { get; init; }

    /// <summary>Its footprint and height, from its logic: x and y in tiles, z in tile heights.</summary>
    public double? DimensionX { get; init; }

    public double? DimensionY { get; init; }

    public double? DimensionZ { get; init; }

    /// <summary>The ways it can face, in degrees (90, 180, ...), from its logic.</summary>
    public required IReadOnlyList<int> Directions { get; init; }

    /// <summary>The colour ids its visualization tints (a furniture's <c>*N</c> variants).</summary>
    public required IReadOnlyList<int> Colors { get; init; }

    /// <summary>Layers in its largest visualization.</summary>
    public int? LayerCount { get; init; }

    /// <summary>The visualization sizes it has (32, 64).</summary>
    public required IReadOnlyList<int> Sizes { get; init; }
}
