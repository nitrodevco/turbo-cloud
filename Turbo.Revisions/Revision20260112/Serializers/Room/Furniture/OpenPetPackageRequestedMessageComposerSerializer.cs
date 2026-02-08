using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class OpenPetPackageRequestedMessageComposerSerializer(int header)
    : AbstractSerializer<OpenPetPackageRequestedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        OpenPetPackageRequestedMessageComposer message
    )
    {
        //
    }
}
