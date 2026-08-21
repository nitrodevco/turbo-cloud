using Turbo.Primitives.Messages.Incoming.Game.Lobby;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Game.Lobby;

internal class GetUserGameAchievementsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetUserGameAchievementsMessage();
}
