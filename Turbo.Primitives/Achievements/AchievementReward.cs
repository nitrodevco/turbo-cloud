using Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Primitives.Achievements;

/// <summary>Handler and version are frozen with the award; payload is data, never executable code.</summary>
[GenerateSerializer, Immutable]
public sealed record AchievementReward
{
    [Id(0)]
    public string Handler { get; init; } = "wallet";

    [Id(1)]
    public int Version { get; init; } = 1;

    [Id(2)]
    public CurrencyKind? Currency { get; init; }

    [Id(3)]
    public int Amount { get; init; }

    [Id(4)]
    public string Payload { get; init; } = "{}";
}
