using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Badges;

internal class BadgeReceivedEventMessageComposerSerializer(int header)
    : AbstractSerializer<BadgeReceivedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BadgeReceivedEventMessageComposer message
    )
    {
        //
    }
}
