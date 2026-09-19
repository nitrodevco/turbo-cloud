using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges;

internal class BadgesEventMessageComposerSerializer(int header)
    : AbstractSerializer<BadgesEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BadgesEventMessageComposer message)
    {
        packet
            .WriteInteger(message.TotalFragments)
            .WriteInteger(message.FragmentNo)
            .WriteInteger(message.Badges.Length);

        foreach (var badge in message.Badges)
            packet
                .WriteInteger(badge.BadgeId)
                .WriteString(badge.BadgeCode)
                .WriteInteger(badge.OwnerCount)
                .WriteInteger((int)badge.Rarity);
    }
}
