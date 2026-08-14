using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class BlockUserUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<BlockUserUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, BlockUserUpdateMessageComposer message)
    {
        packet.WriteInteger(message.Result);
        packet.WriteInteger(message.UserId);
    }
}
