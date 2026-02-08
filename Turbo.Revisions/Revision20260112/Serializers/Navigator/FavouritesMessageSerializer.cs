using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class FavouritesMessageSerializer(int header)
    : AbstractSerializer<FavouritesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FavouritesMessageComposer message)
    {
        packet.WriteInteger(message.Limit).WriteInteger(message.FavoriteRoomIds.Length);

        foreach (var roomId in message.FavoriteRoomIds)
            packet.WriteInteger(roomId);
    }
}
