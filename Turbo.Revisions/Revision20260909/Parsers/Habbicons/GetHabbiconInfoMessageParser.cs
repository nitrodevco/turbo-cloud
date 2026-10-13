using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Habbicons;

internal class GetHabbiconInfoMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetHabbiconInfoMessage { HabbiconId = packet.PopInt() };
}
