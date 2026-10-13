using Orleans;
using Turbo.Primitives.Habbicons.Enums;

namespace Turbo.Primitives.Habbicons.Snapshots;

/// <summary>A Habbicon the player has, and in which state (AS3 <c>_-J1x</c>, parser <c>_-Lm</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record OwnedHabbiconSnapshot
{
    [Id(0)]
    public required int HabbiconId { get; init; }

    [Id(1)]
    public required HabbiconState State { get; init; }
}
