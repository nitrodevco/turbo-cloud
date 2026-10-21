using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

/// <summary>
/// AS3 <c>WiredUserPermanentVariablesList</c>: the owner's id, name and figure follow for any
/// entity type but a user's.
/// </summary>
internal class WiredUserPermanentVariablesMessageComposerSerializer(int header)
    : AbstractSerializer<WiredUserPermanentVariablesMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredUserPermanentVariablesMessageComposer message
    )
    {
        var holder = message.Holder;

        packet
            .WriteInteger(holder.EntityType)
            .WriteInteger(holder.EntityId)
            .WriteString(holder.EntityName)
            .WriteString(holder.EntityFigure);

        if (holder.EntityType != (int)RoomObjectType.Player)
            packet
                .WriteInteger(holder.OwnerId)
                .WriteString(holder.OwnerName)
                .WriteString(holder.OwnerFigure);

        packet.WriteInteger(holder.Variables.Length);

        foreach (var variable in holder.Variables)
            WiredStoredVariableValueSerializer.Serialize(packet, variable, withVariableId: true);
    }
}
