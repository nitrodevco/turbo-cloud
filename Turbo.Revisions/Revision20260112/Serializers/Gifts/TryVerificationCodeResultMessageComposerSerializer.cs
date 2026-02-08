using Turbo.Primitives.Messages.Outgoing.Gifts;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Gifts;

internal class TryVerificationCodeResultMessageComposerSerializer(int header)
    : AbstractSerializer<TryVerificationCodeResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TryVerificationCodeResultMessageComposer message
    )
    {
        //
    }
}
