using System;
using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Marketplace;

internal class MakeOfferMessageParser : IParser
{
    private const int MAX_ITEMS = 1000;

    public IMessageEvent Parse(IClientPacket packet)
    {
        var price = packet.PopInt();
        var furniType = packet.PopInt();
        var count = packet.PopInt();
        var refs = ImmutableArray.CreateBuilder<int>(Math.Clamp(count, 0, MAX_ITEMS));

        for (var i = 0; i < count && i < MAX_ITEMS; i++)
            refs.Add(packet.PopInt());

        return new MakeOfferMessage
        {
            Price = price,
            FurniType = furniType,
            ItemRefs = refs.ToImmutable(),
        };
    }
}
