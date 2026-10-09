using Orleans;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>One reward of a daily task, in the order the client's reward data reads it.</summary>
[GenerateSerializer, Immutable]
public sealed record DailyTaskRewardSnapshot
{
    [Id(0)]
    public required ProductDisplayType ProductType { get; init; }

    /// <summary>What <see cref="ProductType"/> names: a point type, a badge code, a sprite id.</summary>
    [Id(1)]
    public required string RewardTypeId { get; init; }

    /// <summary>A pet's or a bot's figure; empty otherwise.</summary>
    [Id(2)]
    public required string ExtraParams { get; init; }

    [Id(3)]
    public required int Amount { get; init; }
}
