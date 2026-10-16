using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class ClaimProductResultMessageComposerSerializer(int header)
    : AbstractSerializer<ClaimProductResultMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ClaimProductResultMessageComposer message)
    {
        packet.WriteString(message.ClaimId).WriteInteger((int)message.Result);
    }
}
