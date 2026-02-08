using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildMemberMgmtFailedMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMemberMgmtFailedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuildMemberMgmtFailedMessageComposer message
    )
    {
        //
    }
}
