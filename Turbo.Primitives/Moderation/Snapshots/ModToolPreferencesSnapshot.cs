using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>Where the issue handler window opens: x, y, width, height, the order <c>ModToolPreferences</c> sends them in.</summary>
[GenerateSerializer, Immutable]
public sealed record ModToolPreferencesSnapshot
{
    [Id(0)]
    public required int WindowX { get; init; }

    [Id(1)]
    public required int WindowY { get; init; }

    [Id(2)]
    public required int WindowWidth { get; init; }

    [Id(3)]
    public required int WindowHeight { get; init; }
}
