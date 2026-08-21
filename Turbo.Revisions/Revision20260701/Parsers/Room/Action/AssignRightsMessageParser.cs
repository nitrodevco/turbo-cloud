using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Action;

internal class AssignRightsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new AssignRightsMessage { PlayerId = packet.PopInt() };
}
