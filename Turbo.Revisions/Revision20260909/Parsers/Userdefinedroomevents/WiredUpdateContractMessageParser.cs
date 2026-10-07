using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

/// <summary>
/// The client's <c>AbstractContract.addContentsToComposer</c>: id, short type, rules definition,
/// then the payment or reward contract's own three fields.
/// </summary>
internal class WiredUpdateContractMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var contract = new WiredContractSnapshot
        {
            ContractId = packet.PopInt(),
            Type = (WiredContractType)packet.PopShort(),
            Definition = TradeRequirementRulesDefinitionParser.Parse(packet),
        };

        switch (contract.Type)
        {
            case WiredContractType.Payment:
                contract = contract with
                {
                    PaymentMode = (WiredContractPaymentMode)packet.PopShort(),
                    ReceiveText = packet.PopString(),
                    LayoutType = packet.PopString(),
                };
                break;
            case WiredContractType.Reward:
                contract = contract with
                {
                    RewardCategory = packet.PopShort(),
                    ShowDialog = packet.PopBoolean(),
                    RewardText = packet.PopString(),
                };
                break;
        }

        return new WiredUpdateContractMessage { Contract = contract };
    }
}
