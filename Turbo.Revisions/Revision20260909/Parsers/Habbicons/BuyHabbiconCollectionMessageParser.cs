using Turbo.Primitives.Messages.Incoming.Habbicons;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Habbicons;

internal class BuyHabbiconCollectionMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new BuyHabbiconCollectionMessage { CollectionId = packet.PopInt() };
}
