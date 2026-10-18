using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredlink;

internal class WiredLinkSaveConfigurationMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredLinkRequestMessage
        {
            FurniId = packet.PopInt(),
            Operation = WiredLinkOperations.SAVE,
            NetworkId = packet.PopString(),
            ReceiveLocal = packet.PopBoolean(),
        };
}
