using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Turbo;

internal class TurboWebSessionRequestMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) => new TurboWebSessionRequestMessage();
}
