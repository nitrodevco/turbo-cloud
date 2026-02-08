using Turbo.Primitives.Messages.Outgoing.Gifts;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Gifts;

internal class PhoneCollectionStateMessageComposerSerializer(int header)
    : AbstractSerializer<PhoneCollectionStateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PhoneCollectionStateMessageComposer message
    )
    {
        //
    }
}
