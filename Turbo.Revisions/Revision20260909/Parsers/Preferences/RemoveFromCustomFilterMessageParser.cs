using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Preferences;

internal class RemoveFromCustomFilterMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new RemoveFromCustomFilterMessage { Word = packet.PopString() };
}
