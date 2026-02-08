using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Room.Avatar;

internal class DanceMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new DanceMessage { DanceId = packet.PopInt() };
}
