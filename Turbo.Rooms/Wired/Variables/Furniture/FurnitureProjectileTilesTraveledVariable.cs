using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>How many tiles the flight the client is drawing has crossed so far; the whole way once it has landed.</summary>
public sealed class FurnitureProjectileTilesTraveledVariable(RoomGrain roomGrain)
    : FurnitureProjectileVariable(roomGrain)
{
    protected override string VariableName => "@projectile.animation.tiles_traveled";

    protected override ushort Order => 19;

    protected override WiredVariableFlags Flags =>
        WiredVariableFlags.HasValue | WiredVariableFlags.CanInterceptChanges;

    private protected override bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Parse(flight.GetTilesTravelled(nowMs));

        return true;
    }
}
