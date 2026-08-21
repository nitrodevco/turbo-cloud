using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Game.Score;

internal class GetFriendsWeeklyCompetitiveLeaderboardMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetFriendsWeeklyCompetitiveLeaderboardMessage();
}
