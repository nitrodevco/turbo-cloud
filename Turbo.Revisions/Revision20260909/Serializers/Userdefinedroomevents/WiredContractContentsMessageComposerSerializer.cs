using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

/// <summary>
/// Id, short type, rules definition, then the payment or reward contract's own three fields;
/// a trade contract ends at the definition.
/// </summary>
internal class WiredContractContentsMessageComposerSerializer(int header)
    : AbstractSerializer<WiredContractContentsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredContractContentsMessageComposer message
    )
    {
        var contract = message.Contract;

        packet.WriteInteger(contract.ContractId).WriteShort((short)contract.Type);

        TradeRequirementRulesDefinitionSerializer.Serialize(packet, contract.Definition);

        switch (contract.Type)
        {
            case WiredContractType.Payment:
                packet
                    .WriteShort((short)contract.PaymentMode)
                    .WriteString(contract.ReceiveText)
                    .WriteString(contract.LayoutType);
                break;
            case WiredContractType.Reward:
                packet
                    .WriteShort((short)contract.RewardCategory)
                    .WriteBoolean(contract.ShowDialog)
                    .WriteString(contract.RewardText);
                break;
        }
    }
}
