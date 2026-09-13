using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

public record WiredSetPreferencesMessage : IMessageEvent
{
    public bool WiredMenuButton { get; init; }
    public bool WiredInspectButton { get; init; }
    public bool WiredPlayTestMode { get; init; }
    public int VariableSyntaxMode { get; init; }
    public bool WiredWhisperDisabled { get; init; }
    public bool ShowAllNotifications { get; init; }
    public required string UIStyle { get; init; }
}
