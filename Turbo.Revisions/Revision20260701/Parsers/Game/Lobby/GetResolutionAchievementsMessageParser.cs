using Turbo.Primitives.Messages.Incoming.Game.Lobby;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Game.Lobby;

internal class GetResolutionAchievementsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetResolutionAchievementsMessage();
}
