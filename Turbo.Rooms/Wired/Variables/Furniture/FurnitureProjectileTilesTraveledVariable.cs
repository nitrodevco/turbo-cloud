using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>How many tiles the flight the client is drawing has crossed so far; the whole way once it has landed.</summary>
public sealed class FurnitureProjectileTilesTraveledVariable(RoomGrain roomGrain)
    : FurnitureVariable<IRoomItem>(roomGrain)
{
    protected override string VariableName => "@projectile.animation.tiles_traveled";

    protected override WiredVariableGroupSubBandType SubBandType =>
        WiredVariableGroupSubBandType.Other;

    protected override ushort Order => 19;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanInterceptChanges;

    protected override bool TryGetValueForItem(IRoomItem item, out WiredVariableValue value)
    {
        value = WiredVariableValue.Default;

        // A furni no projectile addon has ever moved holds none of these.

        if (_roomGrain.WiredSystem.GetProjectileFlight(item.ObjectId) is not { } flight)
            return false;

        value = WiredVariableValue.Parse(flight.GetTilesTravelled(_roomGrain.NowMs()));

        return true;
    }
}
