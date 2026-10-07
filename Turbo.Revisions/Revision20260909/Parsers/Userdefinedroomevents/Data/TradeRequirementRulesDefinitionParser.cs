using System.Collections.Immutable;
using Turbo.Primitives.Packets;
using Turbo.Primitives.WiredTrading.Snapshots;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Data;

/// <summary>
/// The give alternatives and the get rule, each behind a presence flag, as the client's
/// <c>TradeRequirementRulesDefinition.addToComposer</c> writes them. Every count goes through
/// <c>PopList</c>, so a count larger than the packet cannot allocate entries that are not there.
/// </summary>
internal static class TradeRequirementRulesDefinitionParser
{
    public static TradeRequirementRulesDefinitionSnapshot Parse(IClientPacket packet)
    {
        // A rule is at least its int node count.
        ImmutableArray<TradeRequirementRuleSnapshot>? youGive = packet.PopBoolean()
            ? [.. packet.PopList(bytesPerItem: 4, TradeRequirementRuleParser.Parse)]
            : null;
        var youGet = packet.PopBoolean() ? TradeRequirementRuleParser.Parse(packet) : null;

        return new TradeRequirementRulesDefinitionSnapshot { YouGive = youGive, YouGet = youGet };
    }
}
