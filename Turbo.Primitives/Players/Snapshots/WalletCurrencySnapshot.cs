using Orleans;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Primitives.Players.Snapshots;

[GenerateSerializer, Immutable]
public sealed record WalletCurrencySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required CurrencyType CurrencyType { get; init; }

    [Id(2)]
    public required int? ActivityPointType { get; init; }

    [Id(3)]
    public required int Amount { get; init; }
}
