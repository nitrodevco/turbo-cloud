using Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Snapshots;

/// <summary>A temporary look shown instead of (or over) a player's saved figure.</summary>
[GenerateSerializer, Immutable]
public sealed record PlayerLookOverrideSnapshot
{
    [Id(0)]
    public required string Figure { get; init; }

    /// <summary>The gender to show; null keeps the player's saved gender.</summary>
    [Id(1)]
    public AvatarGenderType? Gender { get; init; }

    [Id(2)]
    public LookOverrideMode Mode { get; init; } = LookOverrideMode.Replace;
}
