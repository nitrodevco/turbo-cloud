using Turbo.Primitives.Messages.Incoming.Game.Ingame;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Game.Ingame;

internal class Game2SetUserMoveTargetMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new Game2SetUserMoveTargetMessage();
}
