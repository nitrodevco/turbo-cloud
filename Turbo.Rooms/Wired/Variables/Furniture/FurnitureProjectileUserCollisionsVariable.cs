using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Avatars the flight has passed over so far, counted as they stood when it began.</summary>
public sealed class FurnitureProjectileUserCollisionsVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@projectile.animation.user_collisions";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 18;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanInterceptChanges;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        // A furni no projectile addon has ever moved holds none of these.

        if (_roomGrain.WiredSystem.GetProjectileFlight(item.ObjectId) is not { } flight)
            return false;

        value = WiredVariableValue.Parse(flight.GetAvatarCollisions(_roomGrain.NowMs()));

        return true;
    }
}
