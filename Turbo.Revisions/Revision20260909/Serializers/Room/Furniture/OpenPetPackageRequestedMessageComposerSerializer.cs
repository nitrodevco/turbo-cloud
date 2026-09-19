using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Pets.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Furniture;

internal class OpenPetPackageRequestedMessageComposerSerializer(int header)
    : AbstractSerializer<OpenPetPackageRequestedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        OpenPetPackageRequestedMessageComposer message
    )
    {
        packet.WriteInteger(message.ObjectId);

        // The client only reads the figure when bytes remain, so a package without a preview
        // simply ends here.
        if (message.Figure is not null)
            PetFigureSerializer.Serialize(packet, message.Figure);
    }
}
