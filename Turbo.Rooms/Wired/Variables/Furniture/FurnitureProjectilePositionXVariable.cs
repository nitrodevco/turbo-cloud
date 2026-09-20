using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Where along x the projectile looks to be while it flies; @position.x is the tile it already belongs to.</summary>
public sealed class FurnitureProjectilePositionXVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@projectile.animation.position.x";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 16;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        // A furni no projectile addon has ever moved holds none of these.

        if (_roomGrain.WiredSystem.GetProjectileFlight(item.ObjectId) is not { } flight)
            return false;

        value = WiredVariableValue.Parse(flight.GetX(_roomGrain.NowMs()));

        return true;
    }
}
