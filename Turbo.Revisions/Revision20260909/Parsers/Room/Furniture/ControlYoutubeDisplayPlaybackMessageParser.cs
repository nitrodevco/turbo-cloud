using Turbo.Primitives.Messages.Incoming.Room.Furniture;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Room.Furniture;

internal class ControlYoutubeDisplayPlaybackMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new ControlYoutubeDisplayPlaybackMessage
        {
            ObjectId = packet.PopInt(),
            CommandId = packet.PopInt(),
        };
}
