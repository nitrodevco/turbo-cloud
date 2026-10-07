using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents;

internal class WiredTransactionGetChestLogsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var chestId = packet.PopInt();
        var pageSize = packet.PopInt();
        var page = packet.PopInt();

        return new WiredTransactionGetChestLogsMessage
        {
            ChestId = chestId,
            PageSize = pageSize,
            Page = page,
        };
    }
}
