using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Preferences;

internal class SetChatPreferencesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        packet.PopBoolean(); // unused, always false

        return new SetChatPreferencesMessage
        {
            ChatMode = (ChatModeType)packet.PopInt(),
            BubbleWidth = (ChatBubbleWidthType)packet.PopInt(),
            ScrollSpeed = (ChatScrollSpeedType)packet.PopInt(),
        };
    }
}
