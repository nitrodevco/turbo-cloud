using Orleans;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Primitives.Orleans.Snapshots.Vault;

[GenerateSerializer, Immutable]
public sealed record IncomeRewardSnapshot
{
    [Id(0)]
    public required VaultRewardCategoryType RewardCategory { get; init; }

    [Id(1)]
    public required VaultRewardType RewardType { get; init; }

    [Id(2)]
    public required int Amount { get; init; }

    [Id(3)]
    public required string ProductCode { get; init; }
}
