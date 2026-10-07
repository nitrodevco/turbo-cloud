using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

/// <summary>A trade rule: its entries, counted, bounded by what the packet still holds.</summary>
internal static class TradeRequirementRuleParser
{
    public static TradeRequirementRuleSnapshot Parse(IClientPacket packet)
    {
        // A node is at least its byte type and int amount.
        var nodes = packet.PopList(bytesPerItem: 5, TradeRequirementNodeParser.Parse);

        return new TradeRequirementRuleSnapshot { Nodes = [.. nodes] };
    }
}
