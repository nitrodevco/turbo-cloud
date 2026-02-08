using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class BuildersClubPlaceWallItemMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new BuildersClubPlaceWallItemMessage
        {
            PageId = packet.PopInt(),
            OfferId = packet.PopInt(),
            ExtraParam = packet.PopString(),
            Location = packet.PopString(),
        };
}
