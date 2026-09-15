using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Roomsettings;

internal class FlatControllersEventMessageComposerSerializer(int header)
    : AbstractSerializer<FlatControllersEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FlatControllersEventMessageComposer message
    )
    {
        packet.WriteInteger(message.RoomId).WriteInteger(message.Controllers.Length);

        foreach (var controller in message.Controllers)
            packet.WriteInteger(controller.PlayerId).WriteString(controller.Name);
    }
}
