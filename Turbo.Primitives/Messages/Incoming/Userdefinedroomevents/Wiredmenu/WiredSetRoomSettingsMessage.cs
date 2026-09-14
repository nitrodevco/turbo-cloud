using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;

public record WiredSetRoomSettingsMessage : IMessageEvent
{
    public WiredPermissionFlags ModifyPermissionMask { get; init; }
    public WiredPermissionFlags ReadPermissionMask { get; init; }
    public required string Timezone { get; init; }
}
