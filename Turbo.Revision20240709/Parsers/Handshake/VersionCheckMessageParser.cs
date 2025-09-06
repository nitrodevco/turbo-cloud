using Turbo.Packets.Abstractions;
using Turbo.Packets.Incoming.Handshake;
using Turbo.Primitives;

namespace Turbo.Revision20240709.Parsers.Handshake;

public class VersionCheckMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new VersionCheckMessage
        {
            ClientID = packet.PopInt(),
            ClientURL = packet.PopString(),
            ExternalVariablesURL = packet.PopString(),
        };
}
