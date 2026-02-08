using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class UniqueMachineIdMessageSerializer(int header)
    : AbstractSerializer<UniqueMachineIdMessage>(header)
{
    protected override void Serialize(IServerPacket packet, UniqueMachineIdMessage message)
    {
        //
    }
}
