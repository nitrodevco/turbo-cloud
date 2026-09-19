using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed class PlayerWalletLiveState
{
    public required PlayerId PlayerId { get; init; }
    public Dictionary<CurrencyKind, WalletCurrencySnapshot> CurrenciesByKind { get; } = [];
}
