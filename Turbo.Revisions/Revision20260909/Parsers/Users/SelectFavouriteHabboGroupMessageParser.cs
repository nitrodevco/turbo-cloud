using Turbo.Primitives.Guilds;
using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class SelectFavouriteHabboGroupMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SelectFavouriteHabboGroupMessage { GuildId = GuildId.Parse(packet.PopInt()) };
}
