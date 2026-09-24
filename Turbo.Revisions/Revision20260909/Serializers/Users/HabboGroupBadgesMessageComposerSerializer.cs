using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Users;

internal class HabboGroupBadgesMessageComposerSerializer(int header)
    : AbstractSerializer<HabboGroupBadgesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabboGroupBadgesMessageComposer message)
    {
        packet.WriteInteger(message.Guilds.Length);

        foreach (var guild in message.Guilds)
            packet.WriteInteger(guild.GuildId).WriteString(guild.BadgeCode);
    }
}
