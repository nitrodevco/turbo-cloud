using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

[GenerateSerializer, Immutable]
public sealed record CreditVaultStatusMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsUnlocked { get; init; }

    [Id(1)]
    public required int TotalBalance { get; init; }

    [Id(2)]
    public required int WithdrawBalance { get; init; }
}
