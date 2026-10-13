using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Vault;

/// <summary>
/// New earnings wait in a category: Flash's <c>VaultController.onIncomeRewardNotificationMessageEvent</c>
/// (catalog/vault/VaultController.as:139) shows the "notification.earning.new" bubble, lights the
/// purse's dot and asks for the status again when the vault is open.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record IncomeRewardNotificationMessageComposer : IComposer
{
    [Id(0)]
    public required VaultRewardCategoryType RewardCategory { get; init; }
}
