using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class CustomUserNotificationMessageComposerSerializer(int header)
    : AbstractSerializer<CustomUserNotificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CustomUserNotificationMessageComposer message
    )
    {
        packet.WriteInteger((int)message.Code);
    }
}
