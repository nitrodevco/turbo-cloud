using Orleans;

namespace Turbo.Primitives.Players.Wallet;

[GenerateSerializer, Immutable]
public record WalletDebit
{
    [Id(0)]
    public required CurrencyKind CurrencyKind { get; init; }

    [Id(1)]
    public required int Amount { get; init; }
}
