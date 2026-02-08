using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Chat;

internal class ChatMessageComposerSerializer(int header)
    : AbstractSerializer<ChatMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ChatMessageComposer message)
    {
        //
    }
}
