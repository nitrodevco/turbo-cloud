using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// Flash <c>WiredMenuErrorMessageParser</c>: one short error code, 0 to 7. The inspection tab
/// drops a fetch still waiting on 0 (<c>WiredMenuInspectionTab.onWiredMenuError</c>); 7 is the
/// one shown as a notification, <c>wiredmenu.error_message.7</c>
/// (<c>WiredMenuController.SHOW_NOTIFICATION_FOR_ERROR</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredMenuErrorEventMessageComposer : IComposer
{
    [Id(0)]
    public required short ErrorCode { get; init; }
}
