using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

/// <summary>
/// A WIRED Set Highlight as the client draws it (Flash <c>WiredFurnitureHighlightData</c>):
/// type 0 Outline, 1 Soft glow, 2 Darken, 3 Color tint, 4 Brighten, 5 Neon aura; strength 0
/// (off) to 100; a 0xRRGGBB colour; animation 0 Steady, 1 Pulse; easing 0 Instant, 1 Linear, 2
/// Ease in, 3 Ease out, 4 Ease in-out (<c>wiredfurni.params.set_furni_highlight.*</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredHighlightSnapshot
{
    [Id(0)]
    public int Type { get; init; }

    [Id(1)]
    public int Strength { get; init; }

    [Id(2)]
    public int Color { get; init; }

    [Id(3)]
    public int AnimationMode { get; init; }

    [Id(4)]
    public int EasingMode { get; init; }

    /// <summary>Strength 0 takes the highlight away: there is nothing to keep for later.</summary>
    public bool IsOff => Strength <= 0;
}

/// <summary>
/// A WIRED Set Visibility as the client draws it (Flash <c>WiredFurnitureTransparencyData</c>):
/// transparency 0 (opaque) to 100, whether clicks go through, and the easing.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransparencySnapshot
{
    [Id(0)]
    public int Transparency { get; init; }

    [Id(1)]
    public bool AllowClickThrough { get; init; }

    [Id(2)]
    public int EasingMode { get; init; }

    /// <summary>Opaque and clickable is how an object is without the box: nothing to keep.</summary>
    public bool IsOff => Transparency <= 0 && !AllowClickThrough;
}

/// <summary>
/// One highlight of <c>WiredFurniVisualizationState</c> / <c>WiredUserVisualizationState</c>:
/// <see cref="Id"/> is the furni id as wired names it to the client (negative for a wall item)
/// or the avatar's room index.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredHighlightStateEntry
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required WiredHighlightSnapshot Highlight { get; init; }
}

/// <summary>One transparency of <c>WiredFurniVisualizationState</c> / <c>WiredUserVisualizationState</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record WiredTransparencyStateEntry
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required WiredTransparencySnapshot Transparency { get; init; }
}
