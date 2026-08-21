using Turbo.Primitives.Messages.Outgoing.Tracking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Tracking;

internal class LatencyPingResponseMessageSerializer(int header)
    : AbstractSerializer<LatencyPingResponseMessage>(header)
{
    protected override void Serialize(IServerPacket packet, LatencyPingResponseMessage message)
    {
        packet.WriteInteger(message.RequestId);
    }
}
