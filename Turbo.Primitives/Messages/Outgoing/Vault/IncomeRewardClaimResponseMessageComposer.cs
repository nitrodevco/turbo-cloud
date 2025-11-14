using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Vault;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

[GenerateSerializer, Immutable]
public sealed record IncomeRewardClaimResponseMessageComposer : IComposer
{
    [Id(0)]
    public required VaultRewardCategoryType RewardCategory { get; init; }

    [Id(1)]
    public required bool Result { get; init; }
}
