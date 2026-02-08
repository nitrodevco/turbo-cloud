using Turbo.Primitives.Messages.Incoming.Groupforums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Groupforums;

internal class GetThreadsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetThreadsMessage();
}
