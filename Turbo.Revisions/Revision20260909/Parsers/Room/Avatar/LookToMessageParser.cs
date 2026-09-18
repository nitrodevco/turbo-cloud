using Turbo.Primitives.Messages.Incoming.Room.Avatar;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Avatar;

internal class LookToMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new LookToMessage { X = packet.PopInt(), Y = packet.PopInt() };
}
