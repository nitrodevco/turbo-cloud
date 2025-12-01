using System;
using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Orleans.Snapshots.Players;

[GenerateSerializer, Immutable]
public sealed record PlayerSummarySnapshot
{
    [Id(0)]
    public required long PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Motto { get; init; }

    [Id(3)]
    public required string Figure { get; init; }

    [Id(4)]
    public required AvatarGenderType Gender { get; init; }

    [Id(5)]
    public required DateTime CreatedAt { get; init; }
}
