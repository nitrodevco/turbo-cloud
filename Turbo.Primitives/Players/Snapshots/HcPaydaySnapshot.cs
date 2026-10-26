using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots;

/// <summary>
/// A player's HC Payday as the club centre shows it (<c>ScrKickbackData</c>): their club streak and
/// join date, what they spent in the catalogue towards the coming payday in Habbo Club and out of
/// it, the credits that earns them, and the minutes left until it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record HcPaydaySnapshot
{
    /// <summary>Days of the Habbo Club run going on now; 0 for a non-member.</summary>
    [Id(0)]
    public required int CurrentStreakDays { get; init; }

    [Id(1)]
    public required DateTime? FirstSubscribedAt { get; init; }

    /// <summary>The share of <see cref="CreditsSpent"/> the payday gives back.</summary>
    [Id(2)]
    public required double KickbackPercentage { get; init; }

    /// <summary>Catalogue credits spent towards this payday while not in Habbo Club.</summary>
    [Id(3)]
    public required int CreditsMissed { get; init; }

    /// <summary>Credits paydays have paid the player, all told.</summary>
    [Id(4)]
    public required int CreditsRewarded { get; init; }

    /// <summary>Catalogue credits spent towards this payday while in Habbo Club.</summary>
    [Id(5)]
    public required int CreditsSpent { get; init; }

    [Id(6)]
    public required int StreakBonusCredits { get; init; }

    /// <summary>The credits <see cref="CreditsSpent"/> earns at <see cref="KickbackPercentage"/>.</summary>
    [Id(7)]
    public required int SpendRewardCredits { get; init; }

    /// <summary>Whole days to the payday, in minutes.</summary>
    [Id(8)]
    public required int MinutesUntilPayday { get; init; }
}
