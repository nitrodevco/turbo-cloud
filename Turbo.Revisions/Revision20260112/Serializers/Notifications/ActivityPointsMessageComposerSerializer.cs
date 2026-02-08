using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Notifications;

internal class ActivityPointsMessageComposerSerializer(int header)
    : AbstractSerializer<ActivityPointsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ActivityPointsMessageComposer message)
    {
        packet.WriteInteger(message.PointsByCategoryId.Count);

        foreach (var (categoryId, points) in message.PointsByCategoryId)
            packet.WriteInteger(categoryId).WriteInteger(points);
    }
}
