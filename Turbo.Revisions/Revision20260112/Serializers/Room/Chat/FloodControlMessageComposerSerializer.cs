using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Chat;

internal class FloodControlMessageComposerSerializer(int header)
    : AbstractSerializer<FloodControlMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FloodControlMessageComposer message)
    {
        //
    }
}
