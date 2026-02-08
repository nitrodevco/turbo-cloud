using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class RequestSpamWallPostItMessageComposerSerializer(int header)
    : AbstractSerializer<RequestSpamWallPostItMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RequestSpamWallPostItMessageComposer message
    )
    {
        packet.WriteInteger(message.ItemId).WriteString(message.Location);
    }
}
