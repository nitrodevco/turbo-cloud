using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildMemberFurniCountInHQMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMemberFurniCountInHQMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuildMemberFurniCountInHQMessageComposer message
    )
    {
        //
    }
}
