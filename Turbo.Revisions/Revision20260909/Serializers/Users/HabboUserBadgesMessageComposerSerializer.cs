using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class HabboUserBadgesMessageComposerSerializer(int header)
    : AbstractSerializer<HabboUserBadgesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabboUserBadgesMessageComposer message)
    {
        packet.WriteInteger(message.PlayerId).WriteInteger(message.Badges.Length);

        // Owner count and rarity are badge-catalogue data the server does not track yet.
        foreach (var badge in message.Badges)
            packet
                .WriteInteger(badge.SlotId)
                .WriteString(badge.BadgeCode)
                .WriteInteger(0)
                .WriteInteger(0);
    }
}
