using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class UpdateGuildIdentityMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UpdateGuildIdentityMessage
        {
            GuildId = GuildId.Parse(packet.PopInt()),
            Name = packet.PopString(),
            Description = packet.PopString(),
        };
}
