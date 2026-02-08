using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Handshake;

internal class DisconnectReasonEventMessageComposerSerializer(int header)
    : AbstractSerializer<DisconnectReasonEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        DisconnectReasonEventMessageComposer message
    )
    {
        //
    }
}
