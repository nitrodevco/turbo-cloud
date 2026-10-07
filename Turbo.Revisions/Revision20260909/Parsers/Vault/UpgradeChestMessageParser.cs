using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class UpgradeChestMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var upgradeCount = packet.PopInt();

        return new UpgradeChestMessage { ChestId = chestId, UpgradeCount = upgradeCount };
    }
}
