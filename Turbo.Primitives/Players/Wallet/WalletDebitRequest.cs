using Orleans;

namespace Turbo.Primitives.Players.Wallet;

[GenerateSerializer, Immutable]
public sealed record WalletDebitRequest
{
    [Id(0)]
    public required string CurrencyType { get; init; }

    [Id(1)]
    public required int Amount { get; init; }

    [Id(2)]
    public required WalletCurrencyKind CurrencyKind { get; init; }

    [Id(3)]
    public required int ActivityPointType { get; init; }

    [Id(4)]
    public int? CurrencyTypeId { get; init; }
}
