using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class RoomUseHabbiconMessageComposerSerializer(int header)
    : AbstractSerializer<RoomUseHabbiconMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomUseHabbiconMessageComposer message
    ) => packet.WriteInteger(message.RoomIndex).WriteInteger(message.HabbiconId);
}
