using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Preferences;

internal class AccountPreferencesEventMessageComposerSerializer(int header)
    : AbstractSerializer<AccountPreferencesEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AccountPreferencesEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.GenericVolume)
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
            .WriteInteger(message.VariableSyntaxMode)
            .WriteBoolean(message.WiredWhisperDisabled)
            .WriteBoolean(message.ShowAllNotifications)
            .WriteString(message.WiredUIStyle)
            .WriteInteger((int)message.ChatSizePreference)
            .WriteInteger((int)message.ChatMode)
            .WriteInteger((int)message.ChatBubbleWidth)
            .WriteInteger((int)message.ChatScrollSpeed)
            .WriteInteger(message.OnlineIndicatorPreference);
    }
}
