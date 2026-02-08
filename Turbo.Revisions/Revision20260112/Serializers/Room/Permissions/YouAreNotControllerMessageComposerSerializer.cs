using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Permissions;

internal class YouAreNotControllerMessageComposerSerializer(int header)
    : AbstractSerializer<YouAreNotControllerMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        YouAreNotControllerMessageComposer message
    )
    {
        //
    }
}
