using Orleans;

namespace Turbo.Primitives.Furniture.Interactions;

/// <summary>Lock or unlock a wired chest, and set how full it may get.</summary>
[GenerateSerializer, Immutable]
public sealed record SetChestOptionsInteraction : FurnitureInteraction
{
    [Id(0)]
    public required bool Locked { get; init; }

    [Id(1)]
    public required bool AutoLock { get; init; }

    [Id(2)]
    public required int Capacity { get; init; }
}
