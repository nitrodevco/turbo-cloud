using Turbo.Primitives.Messages.Incoming.Hotlooks;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Hotlooks;

internal class GetHotLooksMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new GetHotLooksMessage();
}
