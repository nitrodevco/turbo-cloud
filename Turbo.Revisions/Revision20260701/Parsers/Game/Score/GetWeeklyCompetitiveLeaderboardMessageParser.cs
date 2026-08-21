using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Game.Score;

internal class GetWeeklyCompetitiveLeaderboardMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetWeeklyCompetitiveLeaderboardMessage();
}
