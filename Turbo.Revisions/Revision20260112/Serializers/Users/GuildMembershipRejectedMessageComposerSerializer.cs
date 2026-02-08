using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildMembershipRejectedMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMembershipRejectedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuildMembershipRejectedMessageComposer message
    )
    {
        //
    }
}
