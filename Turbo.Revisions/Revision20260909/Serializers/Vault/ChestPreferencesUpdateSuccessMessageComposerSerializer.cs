using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

internal class ChestPreferencesUpdateSuccessMessageComposerSerializer(int header)
    : AbstractSerializer<ChestPreferencesUpdateSuccessMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ChestPreferencesUpdateSuccessMessageComposer message
    )
    {
        packet.WriteInteger(message.ChestId).WriteBoolean(message.IsNotificationPreferences);
    }
}
