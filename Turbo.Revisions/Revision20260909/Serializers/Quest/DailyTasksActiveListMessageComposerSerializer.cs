using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Quest;

internal class DailyTasksActiveListMessageComposerSerializer(int header)
    : AbstractSerializer<DailyTasksActiveListMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        DailyTasksActiveListMessageComposer message
    ) => DailyTaskWriter.WriteTasks(packet, message.Tasks);
}
