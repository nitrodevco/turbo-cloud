using System;
using Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Players.Snapshots;

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
    public required int BadgesRank { get; init; }

    [Id(7)]
    public required bool IsOnline { get; init; }

    [Id(8)]
    public required DateTime CreatedAt { get; init; }

    /// <summary>When the player was last online: now while they are, else the end of their last session.</summary>
    [Id(9)]
    public required DateTime LastSeen { get; init; }

    [Id(10)]
    public required int RespectPoints { get; init; }

    [Id(11)]
    public required int RespectsLeft { get; init; }

    [Id(12)]
    public required int PetRespectsLeft { get; init; }

    [Id(13)]
    public required int RespectReplenishesLeft { get; init; }
}
