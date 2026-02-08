using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildCreatedMessageComposerSerializer(int header)
    : AbstractSerializer<GuildCreatedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildCreatedMessageComposer message)
    {
        //
    }
}
