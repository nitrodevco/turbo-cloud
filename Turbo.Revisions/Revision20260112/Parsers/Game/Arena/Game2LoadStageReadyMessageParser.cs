using Turbo.Primitives.Messages.Incoming.Game.Arena;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Game.Arena;

internal class Game2LoadStageReadyMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new Game2LoadStageReadyMessage();
}
