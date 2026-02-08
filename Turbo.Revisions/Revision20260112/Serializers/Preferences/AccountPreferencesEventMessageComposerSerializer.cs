using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Preferences;

internal class AccountPreferencesEventMessageComposerSerializer(int header)
    : AbstractSerializer<AccountPreferencesEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AccountPreferencesEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.UIVolume)
            .WriteInteger(message.FurniVolume)
            .WriteInteger(message.TraxVolume)
            .WriteBoolean(message.FreeFlowChatDisabled)
            .WriteBoolean(message.RoomInvitesIgnored)
            .WriteBoolean(message.RoomCameraFollowDisabled)
            .WriteInteger((int)message.UIFlags)
            .WriteInteger(message.PreferedChatStyle)
            .WriteBoolean(message.WiredMenuButton)
            .WriteBoolean(message.WiredInspectButton)
            .WriteBoolean(message.PlayTestMode)
            .WriteInteger(message.VariableSyntaxMode);
    }
}
