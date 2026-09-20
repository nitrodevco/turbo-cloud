using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class BuildersClubPlacementWarningMessageComposerSerializer(int header)
    : AbstractSerializer<BuildersClubPlacementWarningMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BuildersClubPlacementWarningMessageComposer message
    )
    {
        packet
            .WriteInteger((int)message.PlacementType)
            .WriteInteger(message.PageId)
            .WriteInteger(message.OfferId)
            .WriteString(message.ExtraParam);

        // The tail is one shape or the other, never both: the client reads it back by the type
        // it was just given.
        if (message.PlacementType == BuildersClubPlacementType.FloorItem)
        {
            packet.WriteInteger(message.X).WriteInteger(message.Y).WriteInteger(message.Direction);

            return;
        }

        packet.WriteString(message.WallLocation);
    }
}
