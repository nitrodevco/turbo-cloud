using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>How high the projectile looks to be while it flies, in hundredths of a tile.</summary>
public sealed class FurnitureProjectilePositionAltitudeVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@projectile.animation.position.altitude";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 14;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        // A furni no projectile addon has ever moved holds none of these.

        if (_roomGrain.WiredSystem.GetProjectileFlight(item.ObjectId) is not { } flight)
            return false;

        value = WiredVariableValue.Parse(flight.GetAltitude(_roomGrain.NowMs()));

        return true;
    }
}
