using Turbo.Primitives.Messages.Outgoing.Handshake;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Handshake;

internal class IdentityAccountsEventMessageComposerSerializer(int header)
    : AbstractSerializer<IdentityAccountsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        IdentityAccountsEventMessageComposer message
    )
    {
        //
    }
}
