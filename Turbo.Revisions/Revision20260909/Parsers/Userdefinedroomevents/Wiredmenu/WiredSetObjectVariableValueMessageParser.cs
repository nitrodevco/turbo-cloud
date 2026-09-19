using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Userdefinedroomevents.Wiredmenu;

internal class WiredSetObjectVariableValueMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var variableTarget = packet.PopInt();
        var objectIdForType = packet.PopInt();
        var variableId = packet.PopString();
        var value = packet.PopInt();
        var operation = packet.PopInt();

        return new WiredSetObjectVariableValueMessage
        {
            VariableTarget = variableTarget,
            ObjectIdForType = objectIdForType,
            VariableId = variableId,
            Value = value,
            Operation = operation,
        };
    }
}
