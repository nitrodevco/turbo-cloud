using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Quest;

internal class ConcurrentUsersGoalProgressMessageComposerSerializer(int header)
    : AbstractSerializer<ConcurrentUsersGoalProgressMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConcurrentUsersGoalProgressMessageComposer message
    )
    {
        //
    }
}
