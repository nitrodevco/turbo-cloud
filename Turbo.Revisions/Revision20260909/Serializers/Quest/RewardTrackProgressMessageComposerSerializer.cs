using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>Track id, task id, the task's count, the track's points.</summary>
internal class RewardTrackProgressMessageComposerSerializer(int header)
    : AbstractSerializer<RewardTrackProgressMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RewardTrackProgressMessageComposer message
    )
    {
        packet.WriteString(message.TrackId);
        packet.WriteString(message.TaskId);
        packet.WriteInteger(message.ProgressCount);
        packet.WriteInteger(message.Points);
    }
}
