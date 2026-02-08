using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Avatar;

internal class CheckUserNameResultMessageComposerSerializer(int header)
    : AbstractSerializer<CheckUserNameResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CheckUserNameResultMessageComposer message
    )
    {
        //
    }
}
