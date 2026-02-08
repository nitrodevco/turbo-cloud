using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Badges;

internal class BadgesEventMessageComposerSerializer(int header)
    : AbstractSerializer<BadgesEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BadgesEventMessageComposer message)
    {
        //
    }
}
