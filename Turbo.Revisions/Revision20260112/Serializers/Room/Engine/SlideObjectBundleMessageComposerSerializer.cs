using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class SlideObjectBundleMessageComposerSerializer(int header)
    : AbstractSerializer<SlideObjectBundleMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        SlideObjectBundleMessageComposer message
    )
    {
        packet
            .WriteInteger(message.FromX)
            .WriteInteger(message.FromY)
            .WriteInteger(message.ToX)
            .WriteInteger(message.ToY)
            .WriteInteger(message.FloorItemHeights.Length);

        foreach (var (objectId, prev, next) in message.FloorItemHeights)
        {
            packet.WriteInteger(objectId).WriteString(prev.ToString()).WriteString(next.ToString());
        }

        packet.WriteInteger(message.RollerItemId);

        if (message.Avatar is not null)
        {
            var (moveType, objectId, prev, next) = message.Avatar.Value;

            packet
                .WriteInteger((int)moveType)
                .WriteInteger(objectId)
                .WriteString(prev.ToString())
                .WriteString(next.ToString());
        }
    }
}
