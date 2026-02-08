using Turbo.Primitives.Messages.Incoming.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Quest;

internal class GetQuestsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetQuestsMessage();
}
