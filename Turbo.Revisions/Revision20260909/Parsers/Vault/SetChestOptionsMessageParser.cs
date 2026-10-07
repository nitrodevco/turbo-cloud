using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class SetChestOptionsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var locked = packet.PopBoolean();
        var autoLock = packet.PopBoolean();
        var capacity = packet.PopInt();

        return new SetChestOptionsMessage
        {
            ChestId = chestId,
            Locked = locked,
            AutoLock = autoLock,
            Capacity = capacity,
        };
    }
}
