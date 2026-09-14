using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Revisions.Revision20260701.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredSetRoomSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredSetRoomSettingsMessage
        {
            ModifyPermissionMask = (WiredPermissionFlags)packet.PopInt(),
            ReadPermissionMask = (WiredPermissionFlags)packet.PopInt(),
            Timezone = packet.PopString(),
        };
}
