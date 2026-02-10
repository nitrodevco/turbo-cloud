using System;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Orleans.Snapshots.Players;

[GenerateSerializer, Immutable]
public sealed record PlayerSummarySnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Motto { get; init; }

    [Id(3)]
    public required string Figure { get; init; }

    [Id(4)]
    public required AvatarGenderType Gender { get; init; }

    [Id(5)]
    public required int AchievementScore { get; init; }

    [Id(6)]
    public required DateTime CreatedAt { get; init; }

    [Id(7)]
    public required int RespectTotal { get; init; }

    [Id(8)]
    public required int RespectLeft { get; init; }

    [Id(9)]
    public required int PetRespectLeft { get; init; }

    [Id(10)]
    public required int RespectReplenishesLeft { get; init; }
}
