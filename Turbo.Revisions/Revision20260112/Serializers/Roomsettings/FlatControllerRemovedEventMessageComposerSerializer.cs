using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Roomsettings;

internal class FlatControllerRemovedEventMessageComposerSerializer(int header)
    : AbstractSerializer<FlatControllerRemovedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FlatControllerRemovedEventMessageComposer message
    )
    {
        //
    }
}
