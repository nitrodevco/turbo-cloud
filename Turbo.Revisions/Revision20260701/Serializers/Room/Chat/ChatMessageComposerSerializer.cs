using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Chat;

internal class ChatMessageComposerSerializer(int header)
    : AbstractSerializer<ChatMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ChatMessageComposer message) =>
        ChatMessagePayloadSerializer.Serialize(packet, message);
}
