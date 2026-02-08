using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Engine;

internal class RemoveItemMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new RemoveItemMessage();
}
