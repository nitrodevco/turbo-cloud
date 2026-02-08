using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Badges;

internal class BadgePointLimitsEventMessageComposerSerializer(int header)
    : AbstractSerializer<BadgePointLimitsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BadgePointLimitsEventMessageComposer message
    )
    {
        packet.WriteInteger(message.LimitsByBadgeCodePrefix.Count);

        foreach (var group in message.LimitsByBadgeCodePrefix)
        {
            packet.WriteString(group.BadgeCodePrefix).WriteInteger(group.Levels.Count);

            foreach (var level in group.Levels)
                packet.WriteInteger(level.Level).WriteInteger(level.Limit);
        }
    }
}
