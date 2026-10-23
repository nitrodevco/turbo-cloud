using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Games.Snapshots;

/// <summary>A lobby as the client's <c>GameLobbyData</c> reads it.</summary>
[GenerateSerializer, Immutable]
public sealed record GameLobbySnapshot
{
    [Id(0)]
    public required int GameId { get; init; }

    [Id(1)]
    public required string LevelName { get; init; }

    [Id(2)]
    public required int GameType { get; init; }

    [Id(3)]
    public required int FieldType { get; init; }

    [Id(4)]
    public required int NumberOfTeams { get; init; }

    [Id(5)]
    public required int MaximumPlayers { get; init; }

    [Id(6)]
    public required string OwningPlayerName { get; init; }

    [Id(7)]
    public required int LevelEntryId { get; init; }

    [Id(8)]
    public required ImmutableArray<GameLobbyPlayerSnapshot> Players { get; init; }
}

/// <summary>A lobby's player as the client's <c>GameLobbyPlayerData</c> reads it.</summary>
[GenerateSerializer, Immutable]
public sealed record GameLobbyPlayerSnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Figure { get; init; }

    [Id(3)]
    public required string Gender { get; init; }

    [Id(4)]
    public required int TeamId { get; init; }

    [Id(5)]
    public int SkillLevel { get; init; }

    [Id(6)]
    public int TotalScore { get; init; }

    [Id(7)]
    public int ScoreToNextLevel { get; init; }
}
