using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Chat;

internal class ShoutMessageComposerSerializer(int header)
    : AbstractSerializer<ShoutMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ShoutMessageComposer message) =>
        ChatMessagePayloadSerializer.Serialize(packet, message);
}
