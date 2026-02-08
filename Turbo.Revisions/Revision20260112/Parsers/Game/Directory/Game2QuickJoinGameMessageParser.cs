using Turbo.Primitives.Messages.Incoming.Game.Directory;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Game.Directory;

internal class Game2QuickJoinGameMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new Game2QuickJoinGameMessage();
}
