using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Action;

internal class LetUserInMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new LetUserInMessage();
}
