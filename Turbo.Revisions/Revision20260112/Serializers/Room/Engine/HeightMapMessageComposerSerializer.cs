using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class HeightMapMessageComposerSerializer(int header)
    : AbstractSerializer<HeightMapMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HeightMapMessageComposer message)
    {
        packet.WriteInteger(message.Width).WriteInteger(message.Size);

        foreach (var height in message.Heights)
            packet.WriteShort(height);
    }
}
