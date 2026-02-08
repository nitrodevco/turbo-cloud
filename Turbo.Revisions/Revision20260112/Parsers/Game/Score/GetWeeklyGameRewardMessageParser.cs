using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Game.Score;

internal class GetWeeklyGameRewardMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetWeeklyGameRewardMessage();
}
