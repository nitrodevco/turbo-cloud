using Turbo.Primitives.Messages.Incoming.Handshake;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Handshake;

internal class CompleteDiffieHandshakeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CompleteDiffieHandshakeMessage { SharedKey = packet.PopString() };
}
