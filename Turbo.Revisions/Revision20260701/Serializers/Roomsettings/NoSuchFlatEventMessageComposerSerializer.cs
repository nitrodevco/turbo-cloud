using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Roomsettings;

internal class NoSuchFlatEventMessageComposerSerializer(int header)
    : AbstractSerializer<NoSuchFlatEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NoSuchFlatEventMessageComposer message)
    {
        //
    }
}
