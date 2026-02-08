using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class ExtendedProfileChangedMessageComposerSerializer(int header)
    : AbstractSerializer<ExtendedProfileChangedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ExtendedProfileChangedMessageComposer message
    )
    {
        packet.WriteInteger(message.UserId);
    }
}
