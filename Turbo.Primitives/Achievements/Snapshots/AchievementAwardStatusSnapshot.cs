using Orleans;

namespace Turbo.Primitives.Achievements.Snapshots;

[GenerateSerializer, Immutable]
public sealed record AchievementAwardStatusSnapshot
{
    [Id(0)]
    public required string AwardKey { get; init; }

    [Id(1)]
    public int DeliveredRewards { get; init; }

    [Id(2)]
    public string? BlockedReason { get; init; }
}
