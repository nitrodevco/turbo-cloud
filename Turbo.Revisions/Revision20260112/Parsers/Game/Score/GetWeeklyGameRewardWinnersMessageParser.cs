using Turbo.Primitives.Messages.Incoming.Game.Score;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Game.Score;

internal class GetWeeklyGameRewardWinnersMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetWeeklyGameRewardWinnersMessage();
}
