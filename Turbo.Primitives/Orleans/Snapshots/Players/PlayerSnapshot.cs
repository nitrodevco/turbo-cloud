using System;
using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Orleans.Snapshots.Players;

[GenerateSerializer, Immutable]
public record PlayerSnapshot
{
    [Id(0)]
    public required long PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; } = string.Empty;

    [Id(2)]
    public required string Motto { get; init; } = string.Empty;

    [Id(3)]
    public required string Figure { get; init; } = string.Empty;

    [Id(4)]
    public required AvatarGenderEnum Gender { get; init; }

    [Id(5)]
    public required DateTime CreatedAt { get; init; }
}
