using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredGetUserPermanentVariablesMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var entityType = packet.PopInt();
        var entityId = packet.PopInt();

        return new WiredGetUserPermanentVariablesMessage
        {
            EntityType = entityType,
            EntityId = entityId,
        };
    }
}
