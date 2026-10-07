using Turbo.Primitives.Messages.Incoming.Vault;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Vault;

internal class SetChestPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var name = packet.PopString();
        var description = packet.PopString();
        var everyoneCanOpen = packet.PopBoolean();
        var everyoneCanDonate = packet.PopBoolean();
        var stateControlMode = packet.PopInt();
        var previewMode = packet.PopInt();
        var previewAmount = packet.PopInt();
        var wiredEnabled = packet.PopBoolean();

        return new SetChestPreferencesMessage
        {
            ChestId = chestId,
            Name = name,
            Description = description,
            EveryoneCanOpen = everyoneCanOpen,
            EveryoneCanDonate = everyoneCanDonate,
            StateControlMode = stateControlMode,
            PreviewMode = previewMode,
            PreviewAmount = previewAmount,
            WiredEnabled = wiredEnabled,
        };
    }
}
