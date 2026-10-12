using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Turbo;

internal class TurboStarterRoomPictureMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var roomId = packet.PopInt();
        var length = packet.PopInt();
        var data = length > 0 ? packet.PopBytes(length) : [];
        var sceneLength = packet.Remaining >= 4 ? packet.PopInt() : 0;

        return new TurboStarterRoomPictureMessage
        {
            RoomId = roomId,
            Data = data,
            Scene = sceneLength > 0 ? packet.PopBytes(sceneLength) : [],
        };
    }
}
