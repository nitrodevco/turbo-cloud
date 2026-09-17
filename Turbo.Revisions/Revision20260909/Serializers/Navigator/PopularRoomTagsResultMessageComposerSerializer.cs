using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator;

internal class PopularRoomTagsResultMessageComposerSerializer(int header)
    : AbstractSerializer<PopularRoomTagsResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PopularRoomTagsResultMessageComposer message
    )
    {
        packet.WriteInteger(message.Tags.Length);

        foreach (var tag in message.Tags)
            packet.WriteString(tag.Tag).WriteInteger(tag.UserCount);
    }
}
