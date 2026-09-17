using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Wired;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

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
            .WriteInteger(item.MoveType == SlideAvatarMoveType.Slide ? 1 : 0) // 0 = mv, otherwise sld
            .WriteInteger(item.AnimationTime)
            .WriteInteger((int)item.BodyDirection)
            .WriteInteger((int)item.HeadDirection)
            .WriteBoolean(item.JumpPower > 0);

        if (item.JumpPower > 0)
            packet.WriteInteger(item.JumpPower);
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
            .WriteInteger((int)item.Rotation)
            .WriteBoolean(false) // has overshooting distance
            .WriteBoolean(false); // has curve strength
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
            .WriteInteger(item.SourceOffsetX)
            .WriteInteger(item.SourceOffsetY)
            .WriteInteger(item.TargetX)
            .WriteInteger(item.TargetY)
            .WriteInteger(item.TargetOffsetX)
            .WriteInteger(item.TargetOffsetY)
            .WriteInteger(item.AnimationTime);
    }

    public static void SerializeUserDirection(IServerPacket packet, WiredUserDirectionSnapshot item)
    {
        packet
            .WriteInteger(item.ObjectId.Value)
            .WriteInteger((int)item.BodyRotation)
            .WriteInteger((int)item.HeadRotation);
    }
}
