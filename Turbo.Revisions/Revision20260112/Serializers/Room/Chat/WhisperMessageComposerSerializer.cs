using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Chat;

internal class WhisperMessageComposerSerializer(int header)
    : AbstractSerializer<WhisperMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WhisperMessageComposer message)
    {
        //
    }
}
