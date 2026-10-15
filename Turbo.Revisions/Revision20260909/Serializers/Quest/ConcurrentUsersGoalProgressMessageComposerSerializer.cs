using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>ConcurrentUsersGoalProgressMessageParser: the state, the users online and the goal.</summary>
internal class ConcurrentUsersGoalProgressMessageComposerSerializer(int header)
    : AbstractSerializer<ConcurrentUsersGoalProgressMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ConcurrentUsersGoalProgressMessageComposer message
    )
    {
        packet.WriteInteger((int)message.Data.State);
        packet.WriteInteger(message.Data.UserCount);
        packet.WriteInteger(message.Data.UserCountGoal);
    }
}
