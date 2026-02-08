using System.Linq;
using Turbo.Primitives.Messages.Outgoing.Perk;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Perk;

internal class PerkAllowancesMessageComposerSerializer(int header)
    : AbstractSerializer<PerkAllowancesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PerkAllowancesMessageComposer message)
    {
        packet.WriteInteger(message.Perks.Count());

        foreach (var perk in message.Perks)
            packet
                .WriteString(perk.Code)
                .WriteString(perk.ErrorMessage)
                .WriteBoolean(perk.IsAllowed);
    }
}
