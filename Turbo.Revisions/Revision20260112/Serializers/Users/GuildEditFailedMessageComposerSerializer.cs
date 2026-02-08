using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class GuildEditFailedMessageComposerSerializer(int header)
    : AbstractSerializer<GuildEditFailedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildEditFailedMessageComposer message)
    {
        //
    }
}
