using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

public class BuildersClubPlaceRoomItemMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new BuildersClubPlaceRoomItemMessage
        {
            PageId = packet.PopInt(),
            OfferId = packet.PopInt(),
            ExtraParam = packet.PopString(),
            X = packet.PopInt(),
            Y = packet.PopInt(),
            Direction = packet.PopInt(),
        };
}
