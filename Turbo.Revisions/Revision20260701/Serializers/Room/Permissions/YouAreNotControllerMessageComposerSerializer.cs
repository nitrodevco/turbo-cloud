using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Permissions;

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
