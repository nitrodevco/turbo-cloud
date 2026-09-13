using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetChatPreferencesMessage : IMessageEvent
{
    public ChatModeType ChatMode { get; init; }
    public ChatBubbleWidthType BubbleWidth { get; init; }
    public ChatScrollSpeedType ScrollSpeed { get; init; }
}
