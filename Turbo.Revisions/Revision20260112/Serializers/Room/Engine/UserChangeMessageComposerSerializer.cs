using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class UserChangeMessageComposerSerializer(int header)
    : AbstractSerializer<UserChangeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserChangeMessageComposer message)
    {
        //
    }
}
