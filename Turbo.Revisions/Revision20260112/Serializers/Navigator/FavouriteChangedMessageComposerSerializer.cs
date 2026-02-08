using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class FavouriteChangedMessageComposerSerializer(int header)
    : AbstractSerializer<FavouriteChangedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FavouriteChangedMessageComposer message)
    {
        packet.WriteInteger(message.RoomId).WriteBoolean(message.Added);
    }
}
