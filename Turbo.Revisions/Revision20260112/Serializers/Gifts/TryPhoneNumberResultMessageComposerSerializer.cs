using Turbo.Primitives.Messages.Outgoing.Gifts;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Gifts;

internal class TryPhoneNumberResultMessageComposerSerializer(int header)
    : AbstractSerializer<TryPhoneNumberResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TryPhoneNumberResultMessageComposer message
    )
    {
        //
    }
}
