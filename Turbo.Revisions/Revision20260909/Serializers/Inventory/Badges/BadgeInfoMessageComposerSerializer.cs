using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges;

internal class BadgeInfoMessageComposerSerializer(int header)
    : AbstractSerializer<BadgeInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BadgeInfoMessageComposer message)
    {
        BadgeEntrySerializer.Serialize(
            packet,
            message.BadgeId,
            message.Info.BadgeCode,
            message.Info.OwnerCount,
            message.Info.Rarity
        );
    }
}
