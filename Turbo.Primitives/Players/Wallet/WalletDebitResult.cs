using Orleans;

namespace Turbo.Primitives.Players.Wallet;

[GenerateSerializer, Immutable]
public sealed record WalletDebitResult
{
    [Id(0)]
    public required bool Succeeded { get; init; }

    [Id(1)]
    public WalletDebitFailure? Failure { get; init; }

    /// <summary>Succeeded because the same debit was made before: nothing was taken now.</summary>
    [Id(2)]
    public bool AlreadyApplied { get; init; }

    public static WalletDebitResult Success() => new() { Succeeded = true };

    public static WalletDebitResult Repeated() => new() { Succeeded = true, AlreadyApplied = true };

    public static WalletDebitResult InsufficientBalance(WalletDebitFailure failure) =>
        new() { Succeeded = false, Failure = failure };
}
