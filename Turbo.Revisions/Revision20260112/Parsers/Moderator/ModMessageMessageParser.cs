using Turbo.Primitives.Messages.Incoming.Moderator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Moderator;

internal class ModMessageMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new ModMessageMessage();
}
