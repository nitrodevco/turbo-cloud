using Turbo.Primitives.Messages.Outgoing.Availability;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Availability;

internal class AvailabilityStatusMessageComposerSerializer(int header)
    : AbstractSerializer<AvailabilityStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        AvailabilityStatusMessageComposer message
    )
    {
        packet
            .WriteBoolean(message.IsOpen)
            .WriteBoolean(message.OnShutDown)
            .WriteBoolean(message.IsAuthenticHabbo);
    }
}
