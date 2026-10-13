using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Crafting;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Revisions.Revision20260909.Parsers.Crafting;

/// <summary>outgoing/crafting/CraftSecretComposer.as:10: the table's id, then the mixer's item ids.</summary>
internal class CraftSecretMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new CraftSecretMessage
        {
            ObjectId = packet.PopInt(),
            ItemIds = CraftingItemIds.Pop(packet),
        };
}

internal static class CraftingItemIds
{
    public static ImmutableArray<RoomObjectId> Pop(IClientPacket packet)
    {
        var count = packet.PopInt();
        var ids = ImmutableArray.CreateBuilder<RoomObjectId>();

        for (var i = 0; i < count; i++)
            ids.Add(packet.PopInt());

        return ids.ToImmutable();
    }
}
