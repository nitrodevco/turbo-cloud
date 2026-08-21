using Turbo.Primitives.Messages.Incoming.Room.Action;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Parsers.Room.Action;

internal class UnmuteUserMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new UnmuteUserMessage { UserId = packet.PopInt(), RoomId = packet.PopInt() };
}
