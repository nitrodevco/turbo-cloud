using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;

namespace Turbo.Rooms.Wired.Variables.Furniture;

/// <summary>Where along x the projectile looks to be while it flies; @position.x is the tile it already belongs to.</summary>
public sealed class FurnitureProjectilePositionXVariable(RoomGrain roomGrain)
    : FurnitureProjectileVariable(roomGrain)
{
    protected override string VariableName => "@projectile.animation.position.x";

    protected override ushort Order => 16;

    protected override WiredVariableFlags Flags => WiredVariableFlags.HasValue;

    private protected override bool TryGetValueForFlight(
        WiredProjectileFlight flight,
        long nowMs,
        out WiredVariableValue value
    )
    {
        value = WiredVariableValue.Parse(flight.GetX(nowMs));

        return true;
    }
}
