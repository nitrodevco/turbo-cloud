using Turbo.Primitives.Messages.Outgoing.Userclassification;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Userclassification;

internal class UserClassificationMessageComposerSerializer(int header)
    : AbstractSerializer<UserClassificationMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        UserClassificationMessageComposer message
    )
    {
        //
    }
}
