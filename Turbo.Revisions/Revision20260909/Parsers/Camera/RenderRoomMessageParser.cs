using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Camera;

internal class RenderRoomMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var length = packet.PopInt();

        return new RenderRoomMessage { Data = length > 0 ? packet.PopBytes(length) : [] };
    }
}
