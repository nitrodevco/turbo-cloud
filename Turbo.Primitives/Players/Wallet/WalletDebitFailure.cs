using Orleans;

namespace Turbo.Primitives.Players.Wallet;

[GenerateSerializer, Immutable]
public sealed record WalletDebitFailure : WalletDebit;
