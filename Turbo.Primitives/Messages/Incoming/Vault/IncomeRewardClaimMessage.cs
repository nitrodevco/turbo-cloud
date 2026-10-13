using Turbo.Primitives.Networking;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Primitives.Messages.Incoming.Vault;

/// <summary>An earnings claim: one category, or <see cref="VaultRewardCategoryType.All"/>.</summary>
public record IncomeRewardClaimMessage : IMessageEvent
{
    public required VaultRewardCategoryType RewardCategory { get; init; }
}
