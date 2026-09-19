using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Inventory.Trading;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Revisions.Revision20260909.Parsers.Inventory.Trading;

internal class AddItemsToTradeMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var count = packet.PopInt();
        var builder = ImmutableArray.CreateBuilder<RoomObjectId>();

        for (var i = 0; i < count; i++)
            builder.Add(packet.PopInt());

        return new AddItemsToTradeMessage { ItemIds = builder.ToImmutable() };
    }
}
