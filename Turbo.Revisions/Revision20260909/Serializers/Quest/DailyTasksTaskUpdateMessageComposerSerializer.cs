using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

/// <summary>Task id (long), repeats, status (byte), seconds left, as the client's update parser reads them.</summary>
internal class DailyTasksTaskUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<DailyTasksTaskUpdateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        DailyTasksTaskUpdateMessageComposer message
    )
    {
        packet.WriteLong(message.TaskId);
        packet.WriteInteger(message.Repeats);
        packet.WriteByte((byte)message.Status);
        packet.WriteInteger(message.SecondsLeft);
    }
}
