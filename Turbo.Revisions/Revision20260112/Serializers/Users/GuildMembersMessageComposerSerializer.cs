using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildMembersMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMembersMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildMembersMessageComposer message)
    {
        //
    }
}
