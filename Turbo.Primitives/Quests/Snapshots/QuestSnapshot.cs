using Orleans;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>
/// One quest as the client's <c>QuestMessageData</c> reads it: where it sits in its campaign,
/// the player's progress on it and what it pays.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record QuestSnapshot
{
    [Id(0)]
    public required string CampaignCode { get; init; }

    [Id(1)]
    public required int CompletedQuestsInCampaign { get; init; }

    [Id(2)]
    public required int QuestCountInCampaign { get; init; }

    [Id(3)]
    public required int ActivityPointType { get; init; }

    [Id(4)]
    public required int Id { get; init; }

    [Id(5)]
    public required bool Accepted { get; init; }

    [Id(6)]
    public required string Type { get; init; }

    [Id(7)]
    public required string ImageVersion { get; init; }

    [Id(8)]
    public required int RewardCurrencyAmount { get; init; }

    [Id(9)]
    public required string LocalizationCode { get; init; }

    [Id(10)]
    public required int CompletedSteps { get; init; }

    [Id(11)]
    public required int TotalSteps { get; init; }

    [Id(12)]
    public required int SortOrder { get; init; }

    [Id(13)]
    public required string CatalogPageName { get; init; }

    [Id(14)]
    public required string ChainCode { get; init; }

    [Id(15)]
    public required bool Easy { get; init; }

    [Id(16)]
    public required bool IsSeasonal { get; init; }

    /// <summary>Sent only for a seasonal quest.</summary>
    [Id(17)]
    public int SecondsLeft { get; init; }
}
