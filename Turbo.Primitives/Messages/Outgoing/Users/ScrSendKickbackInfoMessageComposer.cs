using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Users;

/// <summary>
/// The club centre's membership and HC Payday figures (<c>ScrKickbackData</c>). The streak is in
/// days and the countdown in minutes (<c>ClubCenterView</c> formats them so).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ScrSendKickbackInfoMessageComposer : IComposer
{
    [Id(0)]
    public required int CurrentHcStreak { get; init; }

    [Id(1)]
    public required string FirstSubscriptionDate { get; init; }

    [Id(2)]
    public required double KickbackPercentage { get; init; }

    [Id(3)]
    public required int TotalCreditsMissed { get; init; }

    [Id(4)]
    public required int TotalCreditsRewarded { get; init; }

    [Id(5)]
    public required int TotalCreditsSpent { get; init; }

    [Id(6)]
    public required int CreditRewardForStreakBonus { get; init; }

    [Id(7)]
    public required int CreditRewardForMonthlySpent { get; init; }

    [Id(8)]
    public required int TimeUntilPayday { get; init; }
}
