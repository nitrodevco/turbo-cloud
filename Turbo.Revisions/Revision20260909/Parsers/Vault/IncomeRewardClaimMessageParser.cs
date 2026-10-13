using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Vault.Enums;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

/// <summary>
/// Flash's <c>IncomeRewardClaimMessageComposer</c> sends the category as one signed byte
/// (outgoing/vault/IncomeRewardClaimMessageComposer.as:17), -1 for Claim All.
/// </summary>
internal class IncomeRewardClaimMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new IncomeRewardClaimMessage
        {
            RewardCategory = (VaultRewardCategoryType)(sbyte)packet.PopByte(),
        };
}
