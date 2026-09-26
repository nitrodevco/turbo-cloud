using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class HabboUserBadgesMessageComposerSerializer(int header)
    : AbstractSerializer<HabboUserBadgesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabboUserBadgesMessageComposer message)
    {
        packet.WriteInteger(message.PlayerId).WriteInteger(message.Badges.Length);

        foreach (var badge in message.Badges)
            // A worn badge's row carries the slot where the inventory's carries the id.
            BadgeEntrySerializer.Serialize(
                packet,
                badge.SlotId,
                badge.BadgeCode,
                badge.OwnerCount,
                badge.Rarity
            );
    }
}
