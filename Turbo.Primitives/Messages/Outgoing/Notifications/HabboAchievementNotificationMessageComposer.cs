using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Notifications;

[GenerateSerializer, Immutable]
public sealed record HabboAchievementNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required int Type { get; init; }

    [Id(1)]
    public required int Level { get; init; }

    [Id(2)]
    public required int BadgeId { get; init; }

    [Id(3)]
    public required string BadgeCode { get; init; }

    [Id(4)]
    public required int PointsTotal { get; init; }

    [Id(5)]
    public required int LevelRewardPoints { get; init; }

    [Id(6)]
    public required int LevelRewardPointType { get; init; }

    [Id(7)]
    public required int BonusPoints { get; init; }

    [Id(8)]
    public required int AchievementId { get; init; }

    [Id(9)]
    public required string RemovedBadgeCode { get; init; }

    [Id(10)]
    public required string Category { get; init; }

    [Id(11)]
    public required bool ShowDialogToUser { get; init; }

    [Id(12)]
    public required int OwnerCount { get; init; }

    [Id(13)]
    public required int BadgeRarityId { get; init; }
}
