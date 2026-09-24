using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Users.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class GuildMembershipsMessageComposerSerializer(int header)
    : AbstractSerializer<GuildMembershipsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, GuildMembershipsMessageComposer message)
    {
        packet.WriteInteger(message.Guilds.Length);

        foreach (var guild in message.Guilds)
            GuildInfoSerializer.Serialize(packet, guild);
    }
}
