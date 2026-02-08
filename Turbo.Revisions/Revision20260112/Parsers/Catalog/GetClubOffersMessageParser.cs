using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Catalog;

internal class GetClubOffersMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new GetClubOffersMessage { RequestSource = (ClubOfferRequestSourceType)packet.PopInt() };
}
