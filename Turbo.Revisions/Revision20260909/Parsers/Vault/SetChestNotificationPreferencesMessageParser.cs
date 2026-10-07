using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class SetChestNotificationPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var notifyMode = packet.PopInt();
        var notifyOnChestFull = packet.PopBoolean();
        var notifyOnDonation = packet.PopBoolean();
        var notifyOnWithdraw = packet.PopBoolean();
        var notifyOnChestEmpty = packet.PopBoolean();
        var notifyOnWiredTransaction = packet.PopBoolean();

        return new SetChestNotificationPreferencesMessage
        {
            ChestId = chestId,
            NotifyMode = notifyMode,
            NotifyOnChestFull = notifyOnChestFull,
            NotifyOnDonation = notifyOnDonation,
            NotifyOnWithdraw = notifyOnWithdraw,
            NotifyOnChestEmpty = notifyOnChestEmpty,
            NotifyOnWiredTransaction = notifyOnWiredTransaction,
        };
    }
}
