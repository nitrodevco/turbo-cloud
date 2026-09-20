using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Whether the client is still drawing the flight.</summary>
public sealed class FurnitureProjectileIsTravelingVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@projectile.animation.is_traveling";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 13;

    protected override WiredVariableFlags Flags => WiredVariableFlags.CanInterceptChanges;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        // A furni no projectile addon has ever moved holds none of these.

        if (_roomGrain.WiredSystem.GetProjectileFlight(item.ObjectId) is not { } flight)
            return false;

        value = WiredVariableValue.Parse(flight.IsTravelling(_roomGrain.NowMs()) ? 1 : 0);

        return true;
    }
}
