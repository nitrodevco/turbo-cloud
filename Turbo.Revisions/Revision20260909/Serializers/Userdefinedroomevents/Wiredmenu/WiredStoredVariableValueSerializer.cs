using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents.Wiredmenu;

/// <summary>AS3 <c>WiredVariableStorageParameter</c>; the variable id only where its reader asks for it.</summary>
internal static class WiredStoredVariableValueSerializer
{
    public static void Serialize(
        IServerPacket packet,
        WiredStoredVariableValueSnapshot value,
        bool withVariableId
    )
    {
        if (withVariableId)
            packet.WriteString(value.VariableId);

        packet
            .WriteInteger(value.Value)
            .WriteLong(value.CreatedAtMs)
            .WriteString(value.CreatedAtText)
            .WriteLong(value.UpdatedAtMs)
            .WriteString(value.UpdatedAtText);
    }
}
