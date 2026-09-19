using System.Collections.Generic;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Inventory.Badges;

internal class SetActivatedBadgesMessageParser : IParser
{
    // The client always writes five (slot, code) pairs; the cap only bounds a forged packet.
    private const int MAX_SLOTS = 16;

    public IMessageEvent Parse(IClientPacket packet)
    {
        var badgeCodes = new List<string>();

        while (!packet.End && badgeCodes.Count < MAX_SLOTS)
        {
            // Slots are written 1..n in order, so the position in the list is the slot.
            packet.PopInt();
            badgeCodes.Add(packet.PopString());
        }

        return new SetActivatedBadgesMessage { BadgeCodes = badgeCodes };
    }
}
