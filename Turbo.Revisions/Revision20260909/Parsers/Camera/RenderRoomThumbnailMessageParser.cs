using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Camera;

internal class RenderRoomThumbnailMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var length = packet.PopInt();

        return new RenderRoomThumbnailMessage { Data = length > 0 ? packet.PopBytes(length) : [] };
    }
}
