using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

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

        // Furni, then users, then user turns: the order of every official WiredMovements with
        // more than one kind (official client capture, 254 of 254; wall items were never recorded
        // beside another kind and stay with the furni). The client reads each entry's kind.
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

        foreach (var user in message.Users)
        {
            packet.WriteInteger((int)WiredMovementType.User);

            WiredMovementSerializer.SerializeUserMovement(packet, user);
        }

        foreach (var userDirection in message.UserDirections)
        {
            packet.WriteInteger((int)WiredMovementType.UserDirection);

            WiredMovementSerializer.SerializeUserDirection(packet, userDirection);
        }
    }
}
