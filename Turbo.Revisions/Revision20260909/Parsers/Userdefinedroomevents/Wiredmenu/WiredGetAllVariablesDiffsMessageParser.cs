using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredGetAllVariablesDiffsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var variables = packet.PopList(
            bytesPerItem: 6,
            p => (Id: WiredVariableId.Parse(p.PopString()), Hash: new WiredVariableHash(p.PopInt()))
        );

        return new WiredGetAllVariablesDiffsMessage { VariableIdsWithHash = variables };
    }
}
