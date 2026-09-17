using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Session;

internal class CantConnectMessageComposerSerializer(int header)
    : AbstractSerializer<CantConnectMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CantConnectMessageComposer message)
    {
        packet.WriteInteger((int)message.ErrorType);

        if (message.ErrorType == RoomConnectionErrorType.EnterQueue)
            packet.WriteString(message.AdditionalInfo ?? string.Empty);
    }
}
