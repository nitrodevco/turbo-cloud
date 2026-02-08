using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class WiredMovementsMessageComposerSerializer(int header)
    : AbstractSerializer<WiredMovementsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, WiredMovementsMessageComposer message)
    {
        var updatesCount =
            message.Users.Count
            + message.FloorItems.Count
            + message.WallItems.Count
            + message.UserDirections.Count;

        packet.WriteInteger(updatesCount);

        foreach (var user in message.Users)
        {
            packet.WriteInteger((int)WiredMovementType.User);

            WiredMovementSerializer.SerializeUserMovement(packet, user);
        }

        foreach (var floorItem in message.FloorItems)
        {
            packet.WriteInteger((int)WiredMovementType.FloorItem);

            WiredMovementSerializer.SerializeFloorItemMovement(packet, floorItem);
        }

        foreach (var wallItem in message.WallItems)
        {
            packet.WriteInteger((int)WiredMovementType.WallItem);

            WiredMovementSerializer.SerializeWallItemMovement(packet, wallItem);
        }

        foreach (var userDirection in message.UserDirections)
        {
            packet.WriteInteger((int)WiredMovementType.UserDirection);

            WiredMovementSerializer.SerializeUserDirection(packet, userDirection);
        }
    }
}
