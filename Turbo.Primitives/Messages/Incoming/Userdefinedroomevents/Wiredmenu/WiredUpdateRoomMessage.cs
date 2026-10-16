using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// The wired menu's room state buttons and the <c>:reload</c> / <c>:rollback</c> chat commands
/// (<c>WiredUpdateRoomComposer</c>, sent by <c>WiredMenuSettingsTab.as</c> 82/89 and
/// <c>ChatInputWidgetHandler.as</c> 558/567): false reloads the room, true rolls it back.
/// </summary>
public record WiredUpdateRoomMessage : IMessageEvent
{
    public bool Rollback { get; init; }
}
