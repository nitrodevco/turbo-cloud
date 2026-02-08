using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

namespace Turbo.Revisions.Revision20260112.Serializers.Userdefinedroomevents.Data;

internal class WiredVariableSerializer
{
    public static void Serialize(IServerPacket packet, WiredVariableSnapshot snapshot)
    {
        packet
            .WriteString(snapshot.VariableId.ToString())
            .WriteInteger((int)snapshot.VariableType)
            .WriteString(snapshot.VariableName)
            .WriteInteger((int)snapshot.AvailabilityType)
            .WriteInteger((int)snapshot.TargetType)
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.AlwaysAvailable))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.CanCreateAndDelete))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.HasValue))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.CanWriteValue))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.CanInterceptChanges))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.IsInvisible))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.CanReadCreationTime))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.CanReadLastUpdateTime))
            .WriteBoolean(snapshot.Flags.Has(WiredVariableFlags.HasTextConnector));

        if (snapshot.Flags.Has(WiredVariableFlags.HasTextConnector))
        {
            packet.WriteInteger(snapshot.TextConnectors.Count);

            foreach (var (key, value) in snapshot.TextConnectors)
                packet.WriteInteger(key).WriteString(value);
        }
    }
}
