using Orleans;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Primitives.Players.Wallet;

[GenerateSerializer, Immutable]
public readonly record struct CurrencyKind
{
    [Id(0)]
    public required CurrencyType CurrencyType { get; init; }

    [Id(1)]
    public int? ActivityPointType { get; init; }

    public static CurrencyKind Credits => new() { CurrencyType = CurrencyType.Credits };

    public static CurrencyKind Silver => new() { CurrencyType = CurrencyType.Silver };

    public static CurrencyKind ActivityPoints(int? activityPointType) =>
        new() { CurrencyType = CurrencyType.ActivityPoints, ActivityPointType = activityPointType };
}
