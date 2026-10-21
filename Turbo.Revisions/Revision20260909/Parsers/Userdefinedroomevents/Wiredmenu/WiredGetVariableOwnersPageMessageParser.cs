using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredGetVariableOwnersPageMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var variableId = packet.PopString();
        var page = packet.PopInt();
        var pageSize = packet.PopInt();
        var sortType = packet.PopInt();
        var userTypeFilter = packet.PopInt();

        return new WiredGetVariableOwnersPageMessage
        {
            VariableId = variableId,
            Page = page,
            PageSize = pageSize,
            SortType = sortType,
            UserTypeFilter = userTypeFilter,
        };
    }
}
