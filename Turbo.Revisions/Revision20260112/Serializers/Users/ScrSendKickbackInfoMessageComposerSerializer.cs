using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Users;

internal class ScrSendKickbackInfoMessageComposerSerializer(int header)
    : AbstractSerializer<ScrSendKickbackInfoMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ScrSendKickbackInfoMessageComposer message
    )
    {
        //
    }
}
