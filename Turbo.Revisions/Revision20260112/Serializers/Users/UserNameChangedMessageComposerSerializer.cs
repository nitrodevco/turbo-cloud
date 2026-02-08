using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class UserNameChangedMessageComposerSerializer(int header)
    : AbstractSerializer<UserNameChangedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, UserNameChangedMessageComposer message)
    {
        packet.WriteInteger(message.WebId).WriteInteger(message.Id).WriteString(message.Name);
    }
}
