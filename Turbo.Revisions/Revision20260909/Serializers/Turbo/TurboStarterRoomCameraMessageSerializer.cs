using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboStarterRoomCameraMessageSerializer(int header)
    : AbstractSerializer<TurboStarterRoomCameraMessage>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TurboStarterRoomCameraMessage message
    ) => packet.WriteInteger(message.RoomId);
}
