using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Game.Score;

internal class Game2GetTotalLeaderboardMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var gameTypeId = packet.PopInt();

        return new Game2GetTotalLeaderboardMessage
        {
            GameTypeId = gameTypeId,
            StartRank = packet.PopInt(),
            Direction = packet.PopInt(),
            ViewSize = packet.PopInt(),
            WindowSize = packet.PopInt(),
        };
    }
}
