using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Preferences;

internal class AddToCustomFilterMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new AddToCustomFilterMessage { Word = packet.PopString() };
}
