using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Vault;

internal class CreditVaultStatusMessageComposerSerializer(int header)
    : AbstractSerializer<CreditVaultStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CreditVaultStatusMessageComposer message
    )
    {
        packet
            .WriteBoolean(message.IsUnlocked)
            .WriteInteger(message.TotalBalance)
            .WriteInteger(message.WithdrawBalance);
    }
}
