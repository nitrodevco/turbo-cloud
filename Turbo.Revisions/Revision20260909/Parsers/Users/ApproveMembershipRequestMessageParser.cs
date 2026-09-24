using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class ApproveMembershipRequestMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ApproveMembershipRequestMessage
        {
            GuildId = GuildId.Parse(packet.PopInt()),
            PlayerId = PlayerId.Parse(packet.PopInt()),
        };
}
