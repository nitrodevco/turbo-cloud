using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetChatStylePreferenceMessage : IMessageEvent
{
    public int ChatStyle { get; init; }
    public ChatSizeType FontSize { get; init; }
}
