using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Parsers.Vault.Data;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class WithdrawItemsFromChestMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var itemType = ChestItemTypeParser.Parse(packet);
        var amount = packet.PopInt();

        return new WithdrawItemsFromChestMessage
        {
            ChestId = chestId,
            ItemType = itemType,
            Amount = amount,
        };
    }
}
