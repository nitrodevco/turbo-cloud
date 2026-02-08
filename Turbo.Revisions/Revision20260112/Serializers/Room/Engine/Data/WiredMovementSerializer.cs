using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

internal class WiredMovementSerializer
{
    public static void SerializeUserMovement(IServerPacket packet, WiredUserMovementSnapshot item)
    {
        packet
            .WriteInteger(item.SourceX)
            .WriteInteger(item.SourceY)
            .WriteInteger(item.TargetX)
            .WriteInteger(item.TargetY)
            .WriteString(item.SourceZ.ToString())
            .WriteString(item.TargetZ.ToString())
            .WriteInteger(item.ObjectId.Value)
            .WriteInteger((int)item.MoveType)
            .WriteInteger(item.AnimationTime)
            .WriteInteger((int)item.BodyDirection)
            .WriteInteger((int)item.HeadDirection);
    }

    public static void SerializeFloorItemMovement(
        IServerPacket packet,
        WiredFloorItemMovementSnapshot item
    )
    {
        packet
            .WriteInteger(item.SourceX)
            .WriteInteger(item.SourceY)
            .WriteInteger(item.TargetX)
            .WriteInteger(item.TargetY)
            .WriteString(item.SourceZ.ToString())
            .WriteString(item.TargetZ.ToString())
            .WriteInteger(item.ObjectId.Value)
            .WriteInteger(item.AnimationTime)
            .WriteInteger((int)item.Rotation);
    }

    public static void SerializeWallItemMovement(
        IServerPacket packet,
        WiredWallItemMovementSnapshot item
    )
    {
        packet
            .WriteInteger(item.ObjectId.Value)
            .WriteBoolean(item.IsDirectionRight)
            .WriteInteger(item.SourceX)
            .WriteInteger(item.SourceY)
            .WriteInteger(item.SourceWallOffset)
            .WriteInteger(item.SourceZ)
            .WriteInteger(item.TargetX)
            .WriteInteger(item.TargetY)
            .WriteInteger(item.TargetWallOffset)
            .WriteInteger(item.TargetZ);
    }

    public static void SerializeUserDirection(IServerPacket packet, WiredUserDirectionSnapshot item)
    {
        packet
            .WriteInteger(item.ObjectId.Value)
            .WriteInteger((int)item.BodyRotation)
            .WriteInteger((int)item.HeadRotation);
    }
}
