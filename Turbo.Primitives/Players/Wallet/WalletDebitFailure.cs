using Orleans;

namespace Turbo.Primitives.Players.Wallet;

[GenerateSerializer, Immutable]
public sealed record WalletDebitFailure
{
    [Id(0)]
    public required string CurrencyType { get; init; }

    [Id(1)]
    public required WalletCurrencyKind CurrencyKind { get; init; }

    [Id(2)]
    public required int ActivityPointType { get; init; }
}
