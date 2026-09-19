using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Badges;

internal class BadgeInfoMessageComposerSerializer(int header)
    : AbstractSerializer<BadgeInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BadgeInfoMessageComposer message)
    {
        packet
            .WriteInteger(message.BadgeId)
            .WriteString(message.Info.BadgeCode)
            .WriteInteger(message.Info.OwnerCount)
            .WriteInteger((int)message.Info.Rarity);
    }
}
