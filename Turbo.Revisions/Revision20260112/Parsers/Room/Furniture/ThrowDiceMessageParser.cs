using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Furniture;

internal class ThrowDiceMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new ThrowDiceMessage();
}
