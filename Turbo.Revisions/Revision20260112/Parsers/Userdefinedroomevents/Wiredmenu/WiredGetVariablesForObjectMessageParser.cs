using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredGetVariablesForObjectMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredGetVariablesForObjectMessage()
        {
            SourceType = packet.PopInt(),
            SourceId = packet.PopInt(),
        };
}
