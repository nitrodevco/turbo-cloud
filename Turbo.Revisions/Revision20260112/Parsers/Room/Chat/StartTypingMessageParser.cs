using Turbo.Primitives.Messages.Incoming.Room.Chat;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Chat;

internal class StartTypingMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new StartTypingMessage();
}
