using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.RoomSettings;

internal class GetFlatControllersMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetFlatControllersMessage { RoomId = packet.PopInt() };
}
