using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

internal class DailyTasksTasksAddedMessageComposerSerializer(int header)
    : AbstractSerializer<DailyTasksTasksAddedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        DailyTasksTasksAddedMessageComposer message
    ) => DailyTaskWriter.WriteTasks(packet, message.Tasks);
}
