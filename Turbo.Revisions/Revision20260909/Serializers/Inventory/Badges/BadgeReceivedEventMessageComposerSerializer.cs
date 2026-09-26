using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges;

internal class BadgeReceivedEventMessageComposerSerializer(int header)
    : AbstractSerializer<BadgeReceivedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BadgeReceivedEventMessageComposer message
    )
    {
        BadgeEntrySerializer.Serialize(
            packet,
            message.Badge.BadgeId,
            message.Badge.BadgeCode,
            message.Badge.OwnerCount,
            message.Badge.Rarity
        );
    }
}
