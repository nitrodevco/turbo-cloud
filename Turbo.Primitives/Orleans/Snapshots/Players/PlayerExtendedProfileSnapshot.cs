using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Groups.Snapshots;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Orleans.Snapshots.Players;

[GenerateSerializer, Immutable]
public sealed record PlayerExtendedProfileSnapshot
{
    [Id(0)]
    public required PlayerId UserId { get; init; }

    [Id(1)]
    public required string UserName { get; init; }

    [Id(2)]
    public required string Figure { get; init; }

    [Id(3)]
    public required string Motto { get; init; }

    [Id(4)]
    public required string CreationDate { get; init; }

    [Id(5)]
    public required int AchievementScore { get; init; }

    [Id(6)]
    public required int FriendCount { get; init; }

    [Id(7)]
    public required bool IsFriend { get; init; }

    [Id(8)]
    public required bool IsFriendRequestSent { get; init; }

    [Id(9)]
    public required bool IsOnline { get; init; }

    [Id(10)]
    public required List<GuildInfoSnapshot> Guilds { get; init; }

    [Id(11)]
    public required int LastAccessSinceInSeconds { get; init; }

    [Id(12)]
    public required bool OpenProfileWindow { get; init; }

    [Id(13)]
    public required bool IsHidden { get; init; }

    [Id(14)]
    public required int AccountLevel { get; init; }

    [Id(15)]
    public required int IntegerField24 { get; init; }

    [Id(16)]
    public required int StarGemCount { get; init; }

    [Id(17)]
    public required bool BooleanField26 { get; init; }

    [Id(18)]
    public required bool BooleanField27 { get; init; }
}
