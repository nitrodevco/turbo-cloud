using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Where along y the projectile looks to be while it flies.</summary>
public sealed class FurnitureProjectilePositionYVariable(RoomGrain roomGrain)
    : FurnitureProjectileVariable(roomGrain)
{
    protected override string VariableName => "@projectile.animation.position.y";

    protected override ushort Order => 15;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    private protected override bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Parse(flight.GetY(nowMs));

        return true;
    }
}
