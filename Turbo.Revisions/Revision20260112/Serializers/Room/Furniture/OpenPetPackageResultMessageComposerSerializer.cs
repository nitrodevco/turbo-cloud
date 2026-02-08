using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class OpenPetPackageResultMessageComposerSerializer(int header)
    : AbstractSerializer<OpenPetPackageResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        OpenPetPackageResultMessageComposer message
    )
    {
        packet
            .WriteInteger(message.ObjectId)
            .WriteInteger(message.NameValidationStatus)
            .WriteString(message.NameValidationInfo);
    }
}
