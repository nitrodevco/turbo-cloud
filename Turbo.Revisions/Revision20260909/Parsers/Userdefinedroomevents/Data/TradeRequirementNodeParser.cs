using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Revisions.Revision20260909.Parsers.Vault.Data;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

/// <summary>One trade rule entry as the client's <c>TradeRequirementNode.addToComposer</c> writes it.</summary>
internal static class TradeRequirementNodeParser
{
    public static TradeRequirementNodeSnapshot Parse(IClientPacket packet)
    {
        var type = (TradeRequirementNodeType)packet.PopByte();
        var amount = packet.PopInt();
        var itemType =
            type == TradeRequirementNodeType.Furni ? ChestItemTypeParser.Parse(packet) : null;

        return new TradeRequirementNodeSnapshot
        {
            Type = type,
            Amount = amount,
            ItemType = itemType,
        };
    }
}
