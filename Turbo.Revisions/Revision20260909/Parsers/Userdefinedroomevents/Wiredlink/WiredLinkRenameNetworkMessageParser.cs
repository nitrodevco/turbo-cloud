using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Wired.Link;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredlink;

internal class WiredLinkRenameNetworkMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new WiredLinkRequestMessage
        {
            FurniId = packet.PopInt(),
            Operation = WiredLinkOperations.RENAME_NETWORK,
            NetworkId = packet.PopString(),
            Name = packet.PopString(),
        };
}
