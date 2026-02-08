using Turbo.Primitives.Messages.Outgoing.Nux;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Nux;

internal class NewUserExperienceNotCompleteEventMessageComposerSerializer(int header)
    : AbstractSerializer<NewUserExperienceNotCompleteEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        NewUserExperienceNotCompleteEventMessageComposer message
    )
    {
        //
    }
}
