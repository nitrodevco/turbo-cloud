using Turbo.Primitives.Messages.Incoming.Landingview.Votes;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Landingview.Votes;

internal class CommunityGoalVoteMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new CommunityGoalVoteMessage();
}
