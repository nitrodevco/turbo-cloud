using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Avatar;

internal class SignMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new SignMessage();
}
