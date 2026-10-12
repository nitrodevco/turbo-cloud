using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator;

internal class RoomEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomEventMessageComposer message)
    {
        var evt = message.Event;

        if (evt is null)
        {
            packet
                .WriteInteger(-1)
                .WriteInteger(-1)
                .WriteString(string.Empty)
                .WriteInteger(-1)
                .WriteInteger(-1)
                .WriteString(string.Empty)
                .WriteString(string.Empty)
                .WriteInteger(0)
                .WriteInteger(0)
                .WriteInteger(-1);

            return;
        }

        packet
            .WriteInteger(evt.EventId)
            .WriteInteger(evt.OwnerId)
            .WriteString(evt.OwnerName)
            .WriteInteger(evt.RoomId)
            .WriteInteger((int)RoomEventType.Promotion)
            .WriteString(evt.Name)
            .WriteString(evt.Description)
            .WriteInteger(evt.MinutesSinceCreated(message.SentAtUtc))
            .WriteInteger(evt.MinutesUntilExpiry(message.SentAtUtc))
            .WriteInteger(evt.CategoryId);
    }
}
