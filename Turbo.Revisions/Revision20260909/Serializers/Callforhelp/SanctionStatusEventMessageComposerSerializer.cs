using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Callforhelp;

internal class SanctionStatusEventMessageComposerSerializer(int header)
    : AbstractSerializer<SanctionStatusEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SanctionStatusEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Sanctions.Length);

        foreach (var sanction in message.Sanctions)
        {
            WriteType(packet, sanction.Type);
            packet
                .WriteString(sanction.Description)
                .WriteBoolean(sanction.Gradual)
                .WriteInteger(sanction.ProbationHoursLeft);
            WriteType(packet, sanction.NextType);
        }
    }

    private static void WriteType(IServerPacket packet, SanctionTypeSnapshot type) =>
        packet.WriteString(type.Name).WriteInteger(type.LengthHours).WriteInteger(type.Unknown);
}
