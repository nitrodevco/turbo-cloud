using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredSetUserPermanentVariableMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var entityType = packet.PopInt();
        var entityId = packet.PopInt();
        var variableId = packet.PopString();
        var value = packet.PopInt();
        var operation = packet.PopInt();

        return new WiredSetUserPermanentVariableMessage
        {
            EntityType = entityType,
            EntityId = entityId,
            VariableId = variableId,
            Value = value,
            Operation = operation,
        };
    }
}
