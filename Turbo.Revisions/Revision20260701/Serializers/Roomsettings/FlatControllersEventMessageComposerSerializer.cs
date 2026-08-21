using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Roomsettings;

internal class FlatControllersEventMessageComposerSerializer(int header)
    : AbstractSerializer<FlatControllersEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FlatControllersEventMessageComposer message
    )
    {
        //
    }
}
