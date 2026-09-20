using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.User;

/// <summary>
/// A variable that is one part of where an avatar stands or which way it faces. Writing it
/// puts the avatar there, through the same movement the wired actions use, so the move is
/// announced in the stack's one movement packet and the tile is checked as any other move is.
/// A subclass only says which part it is.
/// </summary>
public abstract class UserPlacementVariable(RoomGrain roomGrain)
    : UserVariable<IRoomAvatar>(roomGrain)
{
    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Position;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue
        | WiredVariableFlags.CanWriteValue
        | WiredVariableFlags.AlwaysAvailable;

    /// <summary>Where the avatar should end up with this part replaced by the value.</summary>
    protected abstract (int X, int Y) ApplyTile(IRoomAvatar avatar, int value);

    /// <summary>True for the part that turns the avatar rather than moving it.</summary>
    protected virtual bool IsRotation => false;

    /// <summary>Which way it should face; null for a value that is no direction at all.</summary>
    protected virtual Rotation? ApplyRotation(int value) => null;

    public override async Task<bool> SetValueAsync(
        IWiredExecutionContext ctx,
        WiredVariableKey key,
        WiredVariableValue value
    )
    {
        if (!CanBind(key) || !TryGetAvatarForKey(key, out var avatar) || avatar is null)
            return false;

        if (IsRotation)
        {
            if (ApplyRotation(value) is not { } rotation)
                return false;

            await ctx.ProcessUserDirectionAsync(avatar, rotation, rotation);

            return true;
        }

        var (x, y) = ApplyTile(avatar, value);

        if (!_roomGrain.MapModule.InBounds(x, y))
            return false;

        return await ctx.ProcessUserMovementAsync(
            avatar,
            _roomGrain.MapModule.ToIdx(x, y),
            SlideAvatarMoveType.Move
        );
    }
}
