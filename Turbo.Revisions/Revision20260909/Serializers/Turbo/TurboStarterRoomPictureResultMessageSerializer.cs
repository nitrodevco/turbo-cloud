using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Turbo;

internal class TurboStarterRoomPictureResultMessageSerializer(int header)
    : AbstractSerializer<TurboStarterRoomPictureResultMessage>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TurboStarterRoomPictureResultMessage message
    ) => packet.WriteInteger(message.RoomId).WriteBoolean(message.Ok);
}
