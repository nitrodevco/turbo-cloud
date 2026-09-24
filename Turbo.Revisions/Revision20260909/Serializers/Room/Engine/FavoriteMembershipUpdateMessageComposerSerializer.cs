using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class FavoriteMembershipUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<FavoriteMembershipUpdateMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FavoriteMembershipUpdateMessageComposer message
    ) =>
        packet
            .WriteInteger(message.RoomIndex)
            .WriteInteger(message.GuildId)
            .WriteInteger(message.Status)
            .WriteString(message.GuildName);
}
