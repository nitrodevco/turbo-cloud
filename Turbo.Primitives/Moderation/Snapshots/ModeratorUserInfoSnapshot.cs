using Orleans;

namespace Turbo.Primitives.Moderation.Snapshots;

/// <summary>
/// What the moderation tool's user info shows (<c>ModeratorUserInfoData</c>, drawn by
/// <c>UserInfoCtrl.refresh</c>). Empty strings are shown as the window's own placeholders.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record ModeratorUserInfoSnapshot
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required string UserName { get; init; }

    [Id(2)]
    public required string Figure { get; init; }

    [Id(3)]
    public required int RegistrationAgeInMinutes { get; init; }

    [Id(4)]
    public required int MinutesSinceLastLogin { get; init; }

    [Id(5)]
    public required bool Online { get; init; }

    [Id(6)]
    public required int CfhCount { get; init; }

    [Id(7)]
    public required int AbusiveCfhCount { get; init; }

    [Id(8)]
    public required int CautionCount { get; init; }

    [Id(9)]
    public required int BanCount { get; init; }

    [Id(10)]
    public required int TradingLockCount { get; init; }

    [Id(11)]
    public required string TradingExpiryDate { get; init; }

    [Id(12)]
    public required string LastPurchaseDate { get; init; }

    [Id(13)]
    public required int IdentityId { get; init; }

    [Id(14)]
    public required int IdentityRelatedBanCount { get; init; }

    [Id(15)]
    public required string PrimaryEmailAddress { get; init; }

    [Id(16)]
    public required string UserClassification { get; init; }

    [Id(17)]
    public required string LastSanctionTime { get; init; }

    /// <summary>Hours since the last sanction; the window reddens its time when within 48.</summary>
    [Id(18)]
    public required int SanctionAgeHours { get; init; }
}
