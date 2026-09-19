using Turbo.Primitives.Messages.Incoming.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Users;

internal class GetBadgeLeaderboardMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetBadgeLeaderboardMessage
        {
            Type = packet.PopInt(),
            Rarity = packet.PopInt(),
            ChunkIndex = packet.PopInt(),
            ChunkSize = packet.PopInt(),
        };
}
