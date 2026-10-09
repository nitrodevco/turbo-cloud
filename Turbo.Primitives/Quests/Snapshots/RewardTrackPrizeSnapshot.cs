using Orleans;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>A reward track prize as the player sees it.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackPrizeSnapshot
{
    [Id(0)]
    public required string Id { get; init; }

    [Id(1)]
    public required int RequiredPoints { get; init; }

    [Id(2)]
    public required ProductDisplayType ProductType { get; init; }

    [Id(3)]
    public required string RewardTypeId { get; init; }

    [Id(4)]
    public required string ExtraParams { get; init; }

    [Id(5)]
    public required int Amount { get; init; }

    [Id(6)]
    public required bool Premium { get; init; }

    /// <summary>Not premium-locked for the player and within their points.</summary>
    [Id(7)]
    public required bool Available { get; init; }

    [Id(8)]
    public required bool Claimed { get; init; }
}
