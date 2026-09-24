using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class UpdateGuildSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateGuildSettingsMessage
        {
            GuildId = GuildId.Parse(packet.PopInt()),
            GuildType = (GuildType)packet.PopInt(),
            RightsLevel = (GuildRightsLevel)packet.PopInt(),
        };
}
