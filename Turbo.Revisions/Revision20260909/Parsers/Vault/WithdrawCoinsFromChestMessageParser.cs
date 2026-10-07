using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class WithdrawCoinsFromChestMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var amount = packet.PopInt();

        return new WithdrawCoinsFromChestMessage { ChestId = chestId, Amount = amount };
    }
}
