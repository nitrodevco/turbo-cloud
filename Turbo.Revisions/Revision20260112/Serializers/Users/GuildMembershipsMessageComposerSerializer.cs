using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildMembershipsMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMembershipsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildMembershipsMessageComposer message)
    {
        //
    }
}
