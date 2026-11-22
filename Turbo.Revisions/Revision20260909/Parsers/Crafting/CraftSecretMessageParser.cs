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
            ItemIds = [.. packet.PopList(bytesPerItem: 4, p => RoomObjectId.Parse(p.PopInt()))],
        };
}
