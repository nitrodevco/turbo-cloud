using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredGetRoomLogsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredGetRoomLogsMessage
        {
            Page = packet.PopInt(),
            PageSize = packet.PopInt(),
            LogLevelFilter = packet.PopInt(),
            LogSourceFilter = packet.PopInt(),
            Query = packet.PopString(),
        };
}
